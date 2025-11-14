using Microsoft.AspNetCore.Mvc;
using ABCRetailers.Models;
using ABCRetailers.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ABCRetailers.Data;


namespace ABCRetailers.Controllers
{
    public class ProductController : Controller
    {
        private readonly IFunctionsApi _api;
        private readonly ILogger<ProductController> _logger;
        private readonly AppDbContext _db;

        public ProductController(IFunctionsApi api, ILogger<ProductController> logger, AppDbContext db)
        {
            _api = api;
            _logger = logger;
            _db = db;
        }
        public async Task<IActionResult> Index(string? searchTerm)
        {
            var products = await _api.Products_ListAsync();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                products = products
                    .Where(p => p.ProductName != null && p.ProductName.ToLower().Contains(searchTerm))
                    .ToList();
            }

            ViewData["CurrentSearch"] = searchTerm;
            return View(products);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            if (product.Price <= 0 && !double.TryParse(product.PriceString, out var parsed))
                ModelState.AddModelError("Price", "Price must be a positive number.");

            if (!ModelState.IsValid) return View(product);

            try
            {
                var created = await _api.Products_CreateAsync(product, imageFile);
                TempData["Success"] = $"Product '{created.ProductName}' created successfully with price {created.PriceString}.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                ModelState.AddModelError("", $"An error occurred while creating the product: {ex.Message}");
                return View(product);
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var product = await _api.Products_GetAsync(id);
            if (product == null) return NotFound();

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Product product, IFormFile? imageFile)
        {
            if (product.Price <= 0 && !double.TryParse(product.PriceString, out var parsed))
                ModelState.AddModelError("Price", "Price must be a positive number.");

            if (!ModelState.IsValid) return View(product);

            try
            {
                var updated = await _api.Products_UpdateAsync(product.RowKey, product, imageFile);
                TempData["Success"] = $"Product '{updated.ProductName}' has been updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product");
                ModelState.AddModelError("", $"An error occurred while updating the product: {ex.Message}");
                return View(product);
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _api.Products_DeleteAsync(id);
                TempData["Success"] = "Product deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while deleting the product: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Customer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(string id, int quantity = 1)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "Invalid product.";
                return RedirectToAction(nameof(Index));
            }

            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                TempData["Error"] = "You must be logged in to add to cart.";
                return RedirectToAction("Login", "Account");
            }

            var product = await _api.Products_GetAsync(id);
            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction(nameof(Index));
            }

            quantity = Math.Max(1, quantity);
            if (product.StockAvailable < quantity)
            {
                TempData["Error"] = $"Only {product.StockAvailable} items available in stock.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var existing = await _db.Cart
                    .SingleOrDefaultAsync(c => c.CustomerUsername == username && c.ProductId == id);

                if (existing == null)
                {
                    var item = new ABCRetailers.Data.CartRecord
                    {
                        CustomerUsername = username,
                        ProductId = id,
                        Quantity = quantity
                    };
                    _db.Cart.Add(item);
                }
                else
                {
                    existing.Quantity += quantity;
                    _db.Cart.Update(existing);
                }

                await _db.SaveChangesAsync();

                TempData["Success"] = $"Added {quantity} × {product.ProductName} to cart.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add to cart");
                TempData["Error"] = "Failed to add item to cart.";
            }

            return RedirectToAction("Index", "Cart");
        }

    }
}




