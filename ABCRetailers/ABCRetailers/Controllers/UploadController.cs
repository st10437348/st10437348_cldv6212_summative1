using ABCRetailers.Models;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetailers.Controllers
{
    [Authorize]
    public class UploadController : Controller
    {
        private readonly IFunctionsApi _api;

        public UploadController(IFunctionsApi api)
        {
            _api = api;
        }

        public IActionResult Index()
        {
            return View(new FileUploadModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(FileUploadModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                if (model.ProofOfPayment != null && model.ProofOfPayment.Length > 0)
                {
                    var fileName = await _api.Uploads_ProofOfPaymentAsync(
                        model.ProofOfPayment,
                        model.OrderId,
                        model.CustomerName
                    );

                    TempData["Success"] = $"File uploaded successfully! File name: {fileName}";
                    return View(new FileUploadModel());
                }

                ModelState.AddModelError("ProofOfPayment", "Please select a file to upload.");
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error uploading file: {ex.Message}");
                return View(model);
            }
        }
    }
}

