using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ABCRetailers.Data;
using ABCRetailers.Models;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace ABCRetailers.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IFunctionsApi _api;
        private readonly ILogger<CartController> _logger;

        public CartController(AppDbContext db, IFunctionsApi api, ILogger<CartController> logger)
        {
            _db = db;
            _api = api;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Account");

            var cartRecords = await _db.Cart.Where(c => c.CustomerUsername == username).ToListAsync();

            var result = new List<(ABCRetailers.Data.CartRecord, ABCRetailers.Models.Product?)>();

            foreach (var rec in cartRecords)
            {
                ABCRetailers.Models.Product? prod = null;
                try { prod = await _api.Products_GetAsync(rec.ProductId); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load product {ProductId} from functions api", rec.ProductId);
                }
                result.Add((rec, prod));
            }

            var tupleList = result.Select(t => (Item: t.Item1, Product: t.Item2)).ToList();
            return View(tupleList);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(string id, int quantity = 1)
        {
            if (string.IsNullOrEmpty(id) || quantity <= 0)
                return BadRequest();

            var username = User.Identity?.Name ?? "";

            ABCRetailers.Models.Product? product = null;
            try { product = await _api.Products_GetAsync(id); } catch { product = null; }
            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("Index", "Product");
            }
            if (product.StockAvailable < quantity)
            {
                TempData["Error"] = $"Only {product.StockAvailable} items available in stock.";
                return RedirectToAction("Index", "Product");
            }

            var existing = await _db.Cart.SingleOrDefaultAsync(c => c.CustomerUsername == username && c.ProductId == id);
            if (existing != null)
            {
                existing.Quantity += quantity;
                _db.Cart.Update(existing);
            }
            else
            {
                var rec = new CartRecord
                {
                    CustomerUsername = username,
                    ProductId = id,
                    Quantity = quantity
                };
                _db.Cart.Add(rec);
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = $"Added {quantity} × {product.ProductName} to cart.";
            return RedirectToAction("Index", "Product");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, int quantity)
        {
            var username = User.Identity?.Name ?? "";
            var rec = await _db.Cart.FindAsync(id);
            if (rec == null || rec.CustomerUsername != username) return NotFound();

            if (quantity <= 0)
            {
                _db.Cart.Remove(rec);
            }
            else
            {
                try
                {
                    var p = await _api.Products_GetAsync(rec.ProductId);
                    if (p != null && quantity > p.StockAvailable)
                    {
                        TempData["Error"] = $"Only {p.StockAvailable} items available in stock. Quantity not updated.";
                        return RedirectToAction(nameof(Index));
                    }
                }
                catch { /* ignore API failure, allow update */ }

                rec.Quantity = quantity;
                _db.Cart.Update(rec);
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Cart updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int id)
        {
            var username = User.Identity?.Name ?? "";
            var rec = await _db.Cart.FindAsync(id);
            if (rec == null || rec.CustomerUsername != username) return NotFound();

            _db.Cart.Remove(rec);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Item removed from cart.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout()
        {
            var username = User.Identity?.Name ?? "";
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Account");

            var cartRows = await _db.Cart.Where(c => c.CustomerUsername == username).ToListAsync();
            if (!cartRows.Any())
            {
                TempData["Error"] = "Cart is empty.";
                return RedirectToAction(nameof(Index));
            }

            string? customerRowKey = null;
            try
            {
                var customers = await _api.Customers_ListAsync();
                var cust = customers.FirstOrDefault(c => string.Equals(c.Username, username, StringComparison.OrdinalIgnoreCase));
                if (cust != null) customerRowKey = cust.RowKey;
            }
            catch
            {
            }

            if (string.IsNullOrEmpty(customerRowKey))
            {
                TempData["Error"] = "Unable to find customer record for current user. Please contact support.";
                return RedirectToAction(nameof(Index));
            }

            bool anyFailed = false;
            foreach (var row in cartRows)
            {
                try
                {
                    var success = await _api.Orders_CreateAsync(customerRowKey, row.ProductId, row.Quantity, DateTime.Now);
                    if (!success) anyFailed = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create order for product {ProductId}", row.ProductId);
                    anyFailed = true;
                }
            }

            if (!anyFailed)
            {
                _db.Cart.RemoveRange(cartRows);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Order placed successfully!";
                return RedirectToAction("Index", "Order");
            }
            else
            {
                TempData["Error"] = "Some items could not be ordered. Please try again or contact support.";
                return RedirectToAction(nameof(Index));
            }
        }

    }
}



