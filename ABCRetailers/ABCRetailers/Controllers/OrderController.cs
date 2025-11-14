using ABCRetailers.Models;
using ABCRetailers.Models.ViewModels;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ABCRetailers.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly IFunctionsApi _api;

        public OrderController(IFunctionsApi api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index()
        {
            var orders = await _api.Orders_ListAsync();

            if (User.Identity?.IsAuthenticated == true && User.IsInRole("Customer"))
            {
                var username = User.Identity?.Name ?? string.Empty;
                orders = orders.Where(o => string.Equals(o.Username, username, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return View(orders);
        }

        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var order = await _api.Orders_GetAsync(id);
            if (order == null) return NotFound();
            return View(order);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            var customers = await _api.Customers_ListAsync();
            var products = await _api.Products_ListAsync();
            return View(new OrderCreateViewModel
            {
                Customers = customers,
                Products = products,
                OrderDate = DateTime.Today
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(OrderCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(model);
                return View(model);
            }

            var product = await _api.Products_GetAsync(model.ProductId);
            if (product == null)
            {
                ModelState.AddModelError("", "Selected product not found.");
                await PopulateDropdowns(model);
                return View(model);
            }
            if (product.StockAvailable < model.Quantity)
            {
                ModelState.AddModelError("Quantity", $"Insufficient stock for product '{product.ProductName}'. Available: {product.StockAvailable}.");
                await PopulateDropdowns(model);
                return View(model);
            }

            var ok = await _api.Orders_CreateAsync(model.CustomerId, model.ProductId, model.Quantity, model.OrderDate);
            if (ok)
            {
                TempData["Success"] = $"Order for product '{product.ProductName}' has been queued.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", "Failed to queue order.");
            await PopulateDropdowns(model);
            return View(model);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var order = await _api.Orders_GetAsync(id);
            if (order == null) return NotFound();
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Order order)
        {
            ModelState.AddModelError("", "Direct order edits are restricted. Use status updates.");
            return View(order);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _api.Orders_DeleteAsync(id);
                TempData["Success"] = "Order deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while deleting the order: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<JsonResult> GetProductPrice(string productId)
        {
            try
            {
                var product = await _api.Products_GetAsync(productId);
                if (product != null)
                {
                    return Json(new { success = true, price = product.Price, stock = product.StockAvailable, productName = product.ProductName });
                }
                return Json(new { success = false });
            }
            catch
            {
                return Json(new { success = false });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<JsonResult> UpdateOrderStatus(string id, string newStatus)
        {
            try
            {
                var ok = await _api.Orders_UpdateStatusAsync(id, newStatus);
                if (ok) return Json(new { success = true, message = $"Order status update queued to {newStatus}." });
                return Json(new { success = false, message = "Failed to queue status update." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        private async Task PopulateDropdowns(OrderCreateViewModel model)
        {
            model.Customers = await _api.Customers_ListAsync();
            model.Products = await _api.Products_ListAsync();
        }
    }
}




