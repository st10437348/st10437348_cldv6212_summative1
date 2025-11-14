using Azure;
using Azure.Data.Tables;

namespace ABCRetailers.Functions.Entities;

public class CustomerEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "Customer";
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
}

public class ProductEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "Product";
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public double Price { get; set; }
    public int StockAvailable { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}

public class OrderEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "Order";
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string CustomerId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.UtcNow;
    public int Quantity { get; set; }
    public double UnitPrice { get; set; }
    public double TotalPrice { get; set; }
    public string Status { get; set; } = "Submitted";
}

