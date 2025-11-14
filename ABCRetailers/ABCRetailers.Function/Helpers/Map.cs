using ABCRetailers.Functions.Entities;
using ABCRetailers.Functions.Models;

namespace ABCRetailers.Functions.Helpers;

public static class Map
{
    public static CustomerDto ToDto(CustomerEntity e) => new()
    {
        id = e.RowKey,
        name = e.Name,
        surname = e.Surname,
        username = e.Username,
        email = e.Email,
        shippingAddress = e.ShippingAddress
    };

    public static void Apply(CustomerDto dto, CustomerEntity e)
    {
        e.Name = dto.name?.Trim() ?? "";
        e.Surname = dto.surname?.Trim() ?? "";
        e.Username = dto.username?.Trim() ?? "";
        e.Email = dto.email?.Trim() ?? "";
        e.ShippingAddress = dto.shippingAddress?.Trim() ?? "";
    }
    public static ProductDto ToDto(ProductEntity e) => new()
    {
        id = e.RowKey,
        productName = e.ProductName,
        description = e.Description,
        price = e.Price,
        stockAvailable = e.StockAvailable,
        imageUrl = e.ImageUrl
    };

    public static void Apply(ProductDto dto, ProductEntity e)
    {
        e.ProductName = dto.productName?.Trim() ?? "";
        e.Description = dto.description?.Trim() ?? "";
        e.Price = dto.price;
        e.StockAvailable = dto.stockAvailable;
        e.ImageUrl = dto.imageUrl ?? "";
    }

    public static OrderDto ToDto(OrderEntity e) => new()
    {
        id = e.RowKey,
        customerId = e.CustomerId,
        username = e.Username,
        productId = e.ProductId,
        productName = e.ProductName,
        orderDate = e.OrderDate,
        quantity = e.Quantity,
        unitPrice = e.UnitPrice,
        totalPrice = e.TotalPrice,
        status = e.Status
    };

    public static void Apply(OrderDto dto, OrderEntity e)
    {
        e.CustomerId = dto.customerId;
        e.Username = dto.username;
        e.ProductId = dto.productId;
        e.ProductName = dto.productName;
        e.OrderDate = dto.orderDate;
        e.Quantity = dto.quantity;
        e.UnitPrice = dto.unitPrice;
        e.TotalPrice = dto.totalPrice;
        e.Status = dto.status;
    }

}
