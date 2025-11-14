using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ABCRetailers.Data;
using ABCRetailers.Models;
using ABCRetailers.Models.ViewModels;
using ABCRetailers.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace ABCRetailers.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IFunctionsApi _api;

        public AccountController(AppDbContext db, IFunctionsApi api)
        {
            _db = db;
            _api = api;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register() => View(new RegisterViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            if (await _db.Users.AnyAsync(u => u.Username == model.Username))
            {
                ModelState.AddModelError("", "Username already exists");
                return View(model);
            }

            if (string.Equals(model.Role, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var customer = new Customer
                    {
                        Name = string.IsNullOrWhiteSpace(model.Name) ? model.Username : model.Name,
                        Surname = model.Surname ?? "",
                        Username = model.Username,
                        Email = model.Email ?? "",
                        ShippingAddress = model.ShippingAddress ?? ""
                    };

                    await _api.Customers_CreateAsync(customer);
                }
                catch
                {
                }
            }

            var user = new User
            {
                Username = model.Username,
                PasswordHash = model.Password,
                Role = model.Role
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Registration complete. Please log in.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string username, string password, string selectedRole)
        {
            var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == username && u.PasswordHash == password);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid username or password");
                return View();
            }

            if (!string.IsNullOrWhiteSpace(selectedRole) &&
                !string.Equals(user.Role, selectedRole, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Selected role does not match account role.");
                return View();
            }
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}



