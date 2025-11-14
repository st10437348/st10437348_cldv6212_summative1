using System.Net.Http.Json;
using System.Net.Http.Headers;
using ABCRetailers.Models;
using Microsoft.Extensions.Options;

namespace ABCRetailers.Services
{
    public class FunctionsOptions
    {
        public string BaseUrl { get; set; } = "https://st10437348part2-hzdpcdccgdghdzc4.southafricanorth-01.azurewebsites.net/api";
    }

    public class FunctionsApiClient : IFunctionsApi
    {
        private readonly HttpClient _http;

        public FunctionsApiClient(HttpClient http, IOptions<FunctionsOptions> opts)
        {
            http.BaseAddress = new Uri(opts.Value.BaseUrl.TrimEnd('/'));
            _http = http;
        }

        public async Task<List<Customer>> Customers_ListAsync()
        {
            var dtos = await _http.GetFromJsonAsync<List<CustomerDto>>("/api/customers") ?? new();
            return dtos.Select(ToCustomer).ToList();
        }

        public async Task<Customer?> Customers_GetAsync(string id)
        {
            var dto = await _http.GetFromJsonAsync<CustomerDto>($"/api/customers/{id}");
            return dto == null ? null : ToCustomer(dto);
        }

        public async Task<Customer> Customers_CreateAsync(Customer customer)
        {
            var resp = await _http.PostAsJsonAsync("/api/customers", new CustomerDto
            {
                name = customer.Name,
                surname = customer.Surname,
                username = customer.Username,
                email = customer.Email,
                shippingAddress = customer.ShippingAddress
            });
            resp.EnsureSuccessStatusCode();
            var dto = await resp.Content.ReadFromJsonAsync<CustomerDto>();
            return ToCustomer(dto!);
        }

        public async Task<Customer> Customers_UpdateAsync(string id, Customer customer)
        {
            var resp = await _http.PutAsJsonAsync($"/api/customers/{id}", new CustomerDto
            {
                name = customer.Name,
                surname = customer.Surname,
                username = customer.Username,
                email = customer.Email,
                shippingAddress = customer.ShippingAddress
            });
            resp.EnsureSuccessStatusCode();
            var dto = await resp.Content.ReadFromJsonAsync<CustomerDto>();
            return ToCustomer(dto!);
        }

        public async Task<bool> Customers_DeleteAsync(string id)
            => (await _http.DeleteAsync($"/api/customers/{id}")).IsSuccessStatusCode;

        private static Customer ToCustomer(CustomerDto d) => new Customer
        {
            PartitionKey = "Customer",
            RowKey = d.id ?? string.Empty,
            Name = d.name ?? string.Empty,
            Surname = d.surname ?? string.Empty,
            Username = d.username ?? string.Empty,
            Email = d.email ?? string.Empty,
            ShippingAddress = d.shippingAddress ?? string.Empty
        };

        private sealed class CustomerDto
        {
            public string? id { get; set; }
            public string? name { get; set; }
            public string? surname { get; set; }
            public string? username { get; set; }
            public string? email { get; set; }
            public string? shippingAddress { get; set; }
        }

        public async Task<List<Product>> Products_ListAsync()
        {
            var dtos = await _http.GetFromJsonAsync<List<ProductDto>>("/api/products") ?? new();
            return dtos.Select(ToProduct).ToList();
        }

        public async Task<Product?> Products_GetAsync(string id)
        {
            var dto = await _http.GetFromJsonAsync<ProductDto>($"/api/products/{id}");
            return dto == null ? null : ToProduct(dto);
        }

        public async Task<Product> Products_CreateAsync(Product product, IFormFile? imageFile)
            => await SendProductAsync(HttpMethod.Post, "/api/products", product, imageFile);

        public async Task<Product> Products_UpdateAsync(string id, Product product, IFormFile? imageFile)
            => await SendProductAsync(HttpMethod.Put, $"/api/products/{id}", product, imageFile);

        public async Task<bool> Products_DeleteAsync(string id)
            => (await _http.DeleteAsync($"/api/products/{id}")).IsSuccessStatusCode;

        private async Task<Product> SendProductAsync(HttpMethod method, string url, Product product, IFormFile? imageFile)
        {
            using var content = new MultipartFormDataContent();

            content.Add(new StringContent(product.ProductName ?? ""), "productName");
            content.Add(new StringContent(product.Description ?? ""), "description");

            var price = product.Price > 0
                ? product.Price
                : (double.TryParse(product.PriceString, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 0);

            content.Add(new StringContent(price.ToString(System.Globalization.CultureInfo.InvariantCulture)), "price");
            content.Add(new StringContent(product.StockAvailable.ToString()), "stockAvailable");

            if (imageFile != null && imageFile.Length > 0)
            {
                var stream = imageFile.OpenReadStream();
                var fileContent = new StreamContent(stream);
                if (!string.IsNullOrWhiteSpace(imageFile.ContentType))
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(imageFile.ContentType);
                content.Add(fileContent, "imageFile", imageFile.FileName);
            }

            using var req = new HttpRequestMessage(method, url) { Content = content };
            var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            var dto = await resp.Content.ReadFromJsonAsync<ProductDto>();
            return ToProduct(dto!);
        }

        private static Product ToProduct(ProductDto d)
        {
            var priceVal = d.price;
            return new Product
            {
                PartitionKey = "Product",
                RowKey = d.id ?? string.Empty,
                ProductName = d.productName ?? string.Empty,
                Description = d.description ?? string.Empty,
                Price = priceVal,
                PriceString = priceVal > 0 ? priceVal.ToString("F2") : "0.00",
                StockAvailable = d.stockAvailable,
                ImageUrl = d.imageUrl ?? string.Empty
            };
        }

        private sealed class ProductDto
        {
            public string? id { get; set; }
            public string? productName { get; set; }
            public string? description { get; set; }
            public double price { get; set; }
            public int stockAvailable { get; set; }
            public string? imageUrl { get; set; }
        }
        public async Task<List<Order>> Orders_ListAsync()
        {
            var dtos = await _http.GetFromJsonAsync<List<OrderDto>>("/api/orders") ?? new();
            return dtos.Select(ToOrder).ToList();
        }

        public async Task<Order?> Orders_GetAsync(string id)
        {
            var dto = await _http.GetFromJsonAsync<OrderDto>($"/api/orders/{id}");
            return dto == null ? null : ToOrder(dto);
        }

        public async Task<bool> Orders_CreateAsync(string customerId, string productId, int quantity, DateTime orderDate)
        {
            var resp = await _http.PostAsJsonAsync("/api/orders", new
            {
                customerId,
                productId,
                quantity,
                orderDate
            });
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> Orders_UpdateStatusAsync(string id, string newStatus)
        {
            var resp = await _http.PostAsJsonAsync($"/api/orders/{id}/status", new { newStatus });
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> Orders_DeleteAsync(string id)
        {
            var resp = await _http.DeleteAsync($"/api/orders/{id}");
            return resp.IsSuccessStatusCode;
        }

        private static Order ToOrder(OrderDto d) => new Order
        {
            PartitionKey = "Order",
            RowKey = d.id ?? string.Empty,
            CustomerId = d.customerId ?? string.Empty,
            Username = d.username ?? string.Empty,
            ProductId = d.productId ?? string.Empty,
            ProductName = d.productName ?? string.Empty,
            OrderDate = d.orderDate,
            Quantity = d.quantity,
            UnitPrice = d.unitPrice,
            TotalPrice = d.totalPrice,
            Status = d.status ?? "Submitted"
        };

        public async Task<string> Uploads_ProofOfPaymentAsync(IFormFile proofOfPayment, string? orderId, string? customerName)
        {
            using var content = new MultipartFormDataContent();

            if (!string.IsNullOrWhiteSpace(orderId))
                content.Add(new StringContent(orderId), "OrderId");
            if (!string.IsNullOrWhiteSpace(customerName))
                content.Add(new StringContent(customerName), "CustomerName");

            var stream = proofOfPayment.OpenReadStream();
            var fileContent = new StreamContent(stream);
            if (!string.IsNullOrWhiteSpace(proofOfPayment.ContentType))
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(proofOfPayment.ContentType);
            content.Add(fileContent, "ProofOfPayment", proofOfPayment.FileName);

            var resp = await _http.PostAsync("/api/uploads/proof-of-payment", content);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadFromJsonAsync<UploadResponse>();
            return json?.fileName ?? proofOfPayment.FileName;
        }


        private sealed class OrderDto
        {
            public string? id { get; set; }
            public string? customerId { get; set; }
            public string? username { get; set; }
            public string? productId { get; set; }
            public string? productName { get; set; }
            public DateTimeOffset orderDate { get; set; }
            public int quantity { get; set; }
            public double unitPrice { get; set; }
            public double totalPrice { get; set; }
            public string? status { get; set; }
        }

        private sealed class UploadResponse
        {
            public string? fileName { get; set; }
            public string? blobUrl { get; set; }
            public string? fileSharePath { get; set; }
            public string? orderId { get; set; }
            public string? customerName { get; set; }
        }


    }
}


