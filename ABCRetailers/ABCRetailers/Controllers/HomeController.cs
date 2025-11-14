using ABCRetailers.Models;
using ABCRetailers.Models.ViewModels;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ABCRetailers.Controllers
{
    public class HomeController : Controller
    {
        private readonly IFunctionsApi _api;

        public HomeController(IFunctionsApi api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _api.Products_ListAsync();
            var customers = await _api.Customers_ListAsync();
            var orders = await _api.Orders_ListAsync();

            var viewModel = new HomeViewModel
            {
                FeaturedProducts = products.Take(5).ToList(),
                ProductCount = products.Count,
                CustomerCount = customers.Count,
                OrderCount = orders.Count
            };
            return View(viewModel);
        }

        public IActionResult Privacy() => View();

        [HttpPost]
        public async Task<IActionResult> InitializeStorage()
        {
            try
            {
                await _api.Customers_ListAsync();
                TempData["Success"] = "Azure Functions reachable and storage verified!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to initialize: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Contact() => View();

        [HttpPost]
        public IActionResult Contact(string name, string email, string message)
        {
            TempData["Success"] = "Thank you for contacting us! We will get back to you shortly.";
            return RedirectToAction(nameof(Contact));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}

