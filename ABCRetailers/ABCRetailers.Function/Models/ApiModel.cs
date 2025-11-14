namespace ABCRetailers.Functions.Models;

public class CustomerDto
{
    public string id { get; set; } = string.Empty;      
    public string name { get; set; } = string.Empty;
    public string surname { get; set; } = string.Empty;
    public string username { get; set; } = string.Empty;
    public string email { get; set; } = string.Empty;
    public string shippingAddress { get; set; } = string.Empty;
}

public class ProductDto
{
    public string id { get; set; } = string.Empty;
    public string productName { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public double price { get; set; }
    public int stockAvailable { get; set; }
    public string imageUrl { get; set; } = string.Empty;
}

public class OrderDto
{
    public string id { get; set; } = string.Empty;
    public string customerId { get; set; } = string.Empty;
    public string username { get; set; } = string.Empty;
    public string productId { get; set; } = string.Empty;
    public string productName { get; set; } = string.Empty;
    public DateTimeOffset orderDate { get; set; } = DateTimeOffset.UtcNow;
    public int quantity { get; set; }
    public double unitPrice { get; set; }
    public double totalPrice { get; set; }
    public string status { get; set; } = "Submitted";
}

public class CreateOrderRequest
{
    public string customerId { get; set; } = string.Empty;
    public string productId { get; set; } = string.Empty;
    public int quantity { get; set; }
    public DateTime orderDate { get; set; } = DateTime.Today;
}

public class UpdateStatusRequest
{
    public string newStatus { get; set; } = string.Empty;
}