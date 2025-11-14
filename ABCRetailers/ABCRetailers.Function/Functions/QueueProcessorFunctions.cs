using System.Globalization;
using System.Text.Json;
using ABCRetailers.Functions.Entities;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailers.Functions.Functions
{
    public class QueueProcessorFunctions
    {
        private static string Conn() =>
            Environment.GetEnvironmentVariable("STORAGE_CONNECTION")
            ?? throw new InvalidOperationException("STORAGE_CONNECTION is not set.");

        private static TableClient Tbl(string name)
        {
            var c = new TableServiceClient(Conn()).GetTableClient(name);
            c.CreateIfNotExists();
            return c;
        }

        // Force Base64 so it matches the trigger
        private static QueueClient Q(string name)
        {
            var opts = new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 };
            var q = new QueueServiceClient(Conn(), opts).GetQueueClient(name);
            q.CreateIfNotExists();
            return q;
        }

        private static readonly string T_ORDERS = Environment.GetEnvironmentVariable("TABLE_ORDERS") ?? "Orders";
        private static readonly string T_PRODUCTS = Environment.GetEnvironmentVariable("TABLE_PRODUCTS") ?? "Products";
        private static readonly string T_CUSTOMERS = Environment.GetEnvironmentVariable("TABLE_CUSTOMERS") ?? "Customers";

        private static readonly string Q_STOCK = Environment.GetEnvironmentVariable("QUEUE_STOCK_UPDATES") ?? "stock-updates";
        private static readonly string Q_ORDERS_ARCHIVE = Environment.GetEnvironmentVariable("QUEUE_ORDER_NOTIFICATIONS_ARCHIVE") ?? "order-notifications-archive";
        private static readonly string Q_STOCK_ARCHIVE = Environment.GetEnvironmentVariable("QUEUE_STOCK_UPDATES_ARCHIVE") ?? "stock-updates-archive";

        [Function("OrderNotifications_Processor")]
        public async Task RunOrderNotifications(
            [QueueTrigger("%QUEUE_ORDER_NOTIFICATIONS%", Connection = "STORAGE_CONNECTION")] string raw,
            FunctionContext ctx)
        {
            var log = ctx.GetLogger("OrderNotifications_Processor");
            log.LogInformation("Received: {Raw}", raw);

            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                if (!root.TryGetProperty("action", out var actEl))
                {
                    log.LogWarning("Missing 'action'.");
                    await ArchiveOrderMessageAsync(raw, log);   // still keep a copy of the que message
                    return;
                }

                var action = actEl.GetString();

                if (string.Equals(action, "create", StringComparison.OrdinalIgnoreCase))
                {
                    if (!root.TryGetProperty("orderId", out var oidEl) ||
                        !root.TryGetProperty("customerId", out var cidEl) ||
                        !root.TryGetProperty("productId", out var pidEl) ||
                        !root.TryGetProperty("quantity", out var qtyEl))
                    {
                        log.LogWarning("Create message missing fields.");
                        await ArchiveOrderMessageAsync(raw, log);
                        return;
                    }

                    var orderId = oidEl.GetString()!;
                    var customerId = cidEl.GetString()!;
                    var productId = pidEl.GetString()!;
                    var quantity = qtyEl.GetInt32();

                    DateTimeOffset orderDate = DateTimeOffset.UtcNow;
                    if (root.TryGetProperty("orderDate", out var odEl))
                    {
                        try
                        {
                            switch (odEl.ValueKind)
                            {
                                case JsonValueKind.String:
                                    {
                                        var s = odEl.GetString();
                                        if (!string.IsNullOrWhiteSpace(s) &&
                                            DateTimeOffset.TryParse(
                                                s,
                                                CultureInfo.InvariantCulture,
                                                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                                out var dto))
                                        {
                                            orderDate = dto;
                                        }
                                        break;
                                    }
                                case JsonValueKind.Number:
                                    orderDate = DateTimeOffset
                                        .FromUnixTimeMilliseconds(odEl.GetInt64())
                                        .ToUniversalTime();
                                    break;
                                default:
                                    try { orderDate = odEl.GetDateTimeOffset().ToUniversalTime(); } catch { }
                                    break;
                            }
                        }
                        catch {}
                    }
                    orderDate = orderDate.ToUniversalTime();

                    var tblOrders = Tbl(T_ORDERS);
                    var tblProducts = Tbl(T_PRODUCTS);
                    var tblCustomers = Tbl(T_CUSTOMERS);

                    // Fetch customer username
                    TableEntity cust;
                    try
                    {
                        cust = (await tblCustomers.GetEntityAsync<TableEntity>("Customer", customerId)).Value;
                    }
                    catch (RequestFailedException ex) when (ex.Status == 404)
                    {
                        log.LogWarning("Customer {Id} not found.", customerId);
                        await ArchiveOrderMessageAsync(raw, log);
                        return;
                    }

                    // Fetch product and validate stock
                    ProductEntity prod;
                    try
                    {
                        prod = (await tblProducts.GetEntityAsync<ProductEntity>("Product", productId)).Value;
                    }
                    catch (RequestFailedException ex) when (ex.Status == 404)
                    {
                        log.LogWarning("Product {Id} not found.", productId);
                        await ArchiveOrderMessageAsync(raw, log);
                        return;
                    }

                    if (prod.StockAvailable < quantity)
                    {
                        log.LogWarning("Insufficient stock for {ProductId}. Stock={Stock}, Requested={Req}",
                            productId, prod.StockAvailable, quantity);
                        await ArchiveOrderMessageAsync(raw, log);
                        return;
                    }

                    // Persist order
                    var order = new OrderEntity
                    {
                        RowKey = orderId,
                        CustomerId = customerId,
                        Username = cust.GetString("Username") ?? "",
                        ProductId = productId,
                        ProductName = prod.ProductName,
                        OrderDate = orderDate,
                        Quantity = quantity,
                        UnitPrice = prod.Price,
                        TotalPrice = prod.Price * quantity,
                        Status = "Submitted"
                    };
                    await tblOrders.AddEntityAsync(order);

                    // Decrement stock
                    var before = prod.StockAvailable;
                    prod.StockAvailable = before - quantity;
                    await tblProducts.UpdateEntityAsync(prod, prod.ETag, TableUpdateMode.Replace);

                    // Emit stock update
                    var stockMsg = new
                    {
                        productId,
                        productName = prod.ProductName,
                        previousStock = before,
                        newStock = prod.StockAvailable,
                        updatedBy = "QueueProcessor",
                        updateDate = DateTime.UtcNow
                    };

                    try
                    {
                        await Q(Q_STOCK).SendMessageAsync(
                            BinaryData.FromString(JsonSerializer.Serialize(stockMsg)));
                    }
                    catch (RequestFailedException ex) when (
                           ex.ErrorCode == "QueueBeingDeleted" || ex.Status == 404 || ex.ErrorCode == "QueueNotFound")
                    {
                        log.LogWarning("Stock queue unavailable ({Code}); skipping stock message.",
                            ex.ErrorCode ?? ex.Status.ToString());
                    }

                    // Archive the original order message to have proof
                    await ArchiveOrderMessageAsync(raw, log);

                    log.LogInformation("Order {OrderId} created. Stock {Prev} -> {New}.", orderId, before, prod.StockAvailable);
                }
                else if (string.Equals(action, "status", StringComparison.OrdinalIgnoreCase))
                {
                    if (!root.TryGetProperty("orderId", out var oidEl) ||
                        !root.TryGetProperty("newStatus", out var stEl))
                    {
                        log.LogWarning("Status message missing fields.");
                        await ArchiveOrderMessageAsync(raw, log);
                        return;
                    }

                    var orderId = oidEl.GetString()!;
                    var newStatus = stEl.GetString()!;

                    var tblOrders = Tbl(T_ORDERS);
                    try
                    {
                        var order = (await tblOrders.GetEntityAsync<OrderEntity>("Order", orderId)).Value;
                        order.Status = newStatus;
                        await tblOrders.UpdateEntityAsync(order, order.ETag, TableUpdateMode.Replace);
                        await ArchiveOrderMessageAsync(raw, log);
                        log.LogInformation("Order {OrderId} status -> {Status}", orderId, newStatus);
                    }
                    catch (RequestFailedException ex) when (ex.Status == 404)
                    {
                        log.LogWarning("Order {OrderId} not found for status update.", orderId);
                        await ArchiveOrderMessageAsync(raw, log);
                    }
                }
                else
                {
                    log.LogWarning("Unknown action '{Action}'.", action);
                    await ArchiveOrderMessageAsync(raw, log);
                }
            }
            catch (Exception ex)
            {
                // Log and archive so raw payload not lost
                log.LogError(ex, "Unhandled error while processing message.");
                await ArchiveOrderMessageAsync(raw, log);
            }
        }

        private static async Task ArchiveOrderMessageAsync(string raw, ILogger log)
        {
            try
            {
                await Q(Q_ORDERS_ARCHIVE).SendMessageAsync(
                    BinaryData.FromString(raw),
                    timeToLive: TimeSpan.FromDays(30)); // keep for 30 days
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to archive order message.");
            }
        }

        [Function("StockUpdates_Processor")]
        public async Task RunStockUpdates(
            [QueueTrigger("%QUEUE_STOCK_UPDATES%", Connection = "STORAGE_CONNECTION")] string message,
            FunctionContext ctx)
        {
            var log = ctx.GetLogger("StockUpdates_Processor");
            log.LogInformation("StockUpdates: {Message}", message);

            // Copy to archive so it persists even though this trigger deletes the original
            try
            {
                await Q(Q_STOCK_ARCHIVE).SendMessageAsync(
                    BinaryData.FromString(message),
                    timeToLive: TimeSpan.FromDays(30));
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to archive stock message.");
            }
        }
    }
}


