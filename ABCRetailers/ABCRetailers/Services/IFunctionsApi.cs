using ABCRetailers.Models;

namespace ABCRetailers.Services;

public interface IFunctionsApi
{
    // Customers
    Task<List<Customer>> Customers_ListAsync();
    Task<Customer?> Customers_GetAsync(string id);
    Task<Customer> Customers_CreateAsync(Customer customer);
    Task<Customer> Customers_UpdateAsync(string id, Customer customer);
    Task<bool> Customers_DeleteAsync(string id);

    // Products
    Task<List<Product>> Products_ListAsync();
    Task<Product?> Products_GetAsync(string id);
    Task<Product> Products_CreateAsync(Product product, IFormFile? imageFile);
    Task<Product> Products_UpdateAsync(string id, Product product, IFormFile? imageFile);
    Task<bool> Products_DeleteAsync(string id);

    // Orders 
    Task<List<Order>> Orders_ListAsync();
    Task<Order?> Orders_GetAsync(string id);
    Task<bool> Orders_CreateAsync(string customerId, string productId, int quantity, DateTime orderDate);
    Task<bool> Orders_UpdateStatusAsync(string id, string newStatus);
    Task<bool> Orders_DeleteAsync(string id);

    // Uploads
    Task<string> Uploads_ProofOfPaymentAsync(IFormFile proofOfPayment, string? orderId, string? customerName);

}



