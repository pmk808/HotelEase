using System.Diagnostics;
using HotelEase.Models;
using Microsoft.AspNetCore.Mvc;

namespace HotelEase.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Form()
        {
            return View();
        }

        [HttpPost]
        public IActionResult SubmitForm(FormModel model)
        {
            if (ModelState.IsValid)
            {
                TempData["SuccessMessage"] = "Form submitted successfully!";
                return RedirectToAction("Payment");
            }

            return View("Form", model);
        }

        // Show the Payment Page
        public IActionResult Payment()
        {
            return View(new Payment()); // Ensure a model is passed
        }

        // Handle Payment Submission
        [HttpPost]
        public IActionResult ProcessPayment(Payment model)
        {
            if (ModelState.IsValid)
            {
                TempData["PaymentSuccess"] = "Payment processed successfully!";
                return RedirectToAction("ConfirmPay");
            }

            return View("Payment", model);
        }

        // Show Payment Confirmation Page
        public IActionResult ConfirmPay()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public IActionResult VerifyPayment(ConfirmPayment model)
        {
            if (ModelState.IsValid)
            {
                TempData["SuccessMessage"] = "Payment confirmed successfully!";
                return RedirectToAction("Index"); // Redirect to homepage or another success page
            }

            return View("ConfirmPay", model);
        }

    }
}
