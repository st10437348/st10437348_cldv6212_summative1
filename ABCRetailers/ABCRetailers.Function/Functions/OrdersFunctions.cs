using System.Net;
using System.Text.Json;
using ABCRetailers.Functions.Entities;
using ABCRetailers.Functions.Helpers;
using ABCRetailers.Functions.Models;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ABCRetailers.Functions.Functions;

public class OrdersFunctions
{
    private readonly TableServiceClient _tableService;
    private readonly QueueServiceClient _queueService;
    private readonly ILogger<OrdersFunctions> _log;
    private readonly string _ordersTable;
    private readonly string _ordersQueue;

    public OrdersFunctions(TableServiceClient tables, QueueServiceClient queues, ILogger<OrdersFunctions> log)
    {
        _tableService = tables;
        _queueService = queues;
        _log = log;
        _ordersTable = Environment.GetEnvironmentVariable("TABLE_ORDERS") ?? "Orders";
        _ordersQueue = Environment.GetEnvironmentVariable("QUEUE_ORDER_NOTIFICATIONS") ?? "order-notifications";
    }

    private TableClient OrdersTable()
    {
        var c = _tableService.GetTableClient(_ordersTable);
        c.CreateIfNotExists();
        return c;
    }

    private QueueClient OrdersQueue()
    {
        var c = _queueService.GetQueueClient(_ordersQueue);
        c.CreateIfNotExists();
        return c;
    }

    [Function("Orders_List")]
    public async Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequestData req)
    {
        var table = OrdersTable();
        var results = new List<OrderDto>();
        await foreach (var e in table.QueryAsync<OrderEntity>(x => x.PartitionKey == "Order"))
            results.Add(Map.ToDto(e));

        return await HttpJson.Ok(req, results);
    }

    [Function("Orders_Get")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{id}")] HttpRequestData req,
        string id)
    {
        var table = OrdersTable();
        try
        {
            var e = (await table.GetEntityAsync<OrderEntity>("Order", id)).Value;
            return await HttpJson.Ok(req, Map.ToDto(e));
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return await HttpJson.Error(req, HttpStatusCode.NotFound, "Order not found.");
        }
    }
    [Function("Orders_Create")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequestData req)
    {
        var body = await JsonSerializer.DeserializeAsync<CreateOrderRequest>(req.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (body is null || string.IsNullOrWhiteSpace(body.customerId)
            || string.IsNullOrWhiteSpace(body.productId) || body.quantity <= 0)
            return await HttpJson.Error(req, HttpStatusCode.BadRequest, "Invalid order payload.");

        var message = new
        {
            action = "create",
            orderId = Guid.NewGuid().ToString(),
            customerId = body.customerId,
            productId = body.productId,
            quantity = body.quantity,
            orderDate = body.orderDate.ToUniversalTime().ToString("O")
        };

        var q = OrdersQueue();
        await q.SendMessageAsync(BinaryData.FromString(JsonSerializer.Serialize(message)));

        return await HttpJson.Ok(req, new { queued = true, message = "Order enqueued for creation." });
    }


    [Function("Orders_UpdateStatus")]
    public async Task<HttpResponseData> UpdateStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "put", "patch", Route = "orders/{id}/status")] HttpRequestData req,
        string id)
    {
        var body = await JsonSerializer.DeserializeAsync<UpdateStatusRequest>(req.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (body is null || string.IsNullOrWhiteSpace(body.newStatus))
            return await HttpJson.Error(req, HttpStatusCode.BadRequest, "newStatus is required.");

        var q = OrdersQueue();
        var message = new { action = "status", orderId = id, newStatus = body.newStatus, updatedDate = DateTime.UtcNow };
        await q.SendMessageAsync(BinaryData.FromString(JsonSerializer.Serialize(message)));

        return await HttpJson.Ok(req, new { queued = true, message = "Status update enqueued." });
    }

    [Function("Orders_Delete")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "orders/{id}")] HttpRequestData req, string id)
    {
        var table = OrdersTable();
        try
        {
            await table.DeleteEntityAsync("Order", id);
            return await HttpJson.Ok(req, new { deleted = true });
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return await HttpJson.Error(req, HttpStatusCode.NotFound, "Order not found.");
        }
    }
}

