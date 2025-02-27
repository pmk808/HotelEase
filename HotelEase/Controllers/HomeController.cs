using System.Diagnostics;
using HotelEase.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotelEase.Data;

namespace HotelEase.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
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

        [HttpGet]
        public async Task<IActionResult> Form(int roomId, DateTime checkInDate, DateTime checkOutDate)
        {
            var room = await _context.Rooms.FindAsync(roomId);
            if (room == null)
            {
                return NotFound();
            }

            // Create a form model with the room details
            var model = new FormModel
            {
                RoomId = room.Id,
                RoomName = room.Name,
                RoomCategory = room.Category,
                RoomBedType = room.BedType,
                RoomPrice = room.Price,
                CheckInDate = checkInDate,
                CheckOutDate = checkOutDate
            };

            // If user is logged in, pre-fill their information
            if (User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    model.FullName = user.FullName;
                    model.Email = user.Email ?? string.Empty;
                }
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitForm(FormModel model)
        {
            if (ModelState.IsValid)
            {
                // Get room details
                var room = await _context.Rooms.FindAsync(model.RoomId);
                if (room == null)
                {
                    ModelState.AddModelError("", "Selected room not found.");
                    return View("Form", model);
                }

                // Calculate total nights and price
                int nights = (int)(model.CheckOutDate - model.CheckInDate).TotalDays;
                decimal totalPrice = room.Price * nights;

                // Create booking in pending status
                var booking = new Booking
                {
                    RoomId = model.RoomId,
                    CheckInDate = model.CheckInDate,
                    CheckOutDate = model.CheckOutDate,
                    TotalPrice = totalPrice,
                    Status = BookingStatus.Pending,
                    BookingDate = DateTime.Now
                };

                // If user is logged in, associate booking with their account
                if (User.Identity.IsAuthenticated)
                {
                    var user = await _userManager.GetUserAsync(User);
                    if (user != null)
                    {
                        booking.UserId = user.Id;
                    }
                }

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                // Store the booking ID in TempData for the payment page
                TempData["BookingId"] = booking.Id;

                // Convert decimal to string before storing in TempData
                TempData["TotalPriceString"] = totalPrice.ToString();

                TempData["SuccessMessage"] = "Booking information submitted successfully!";

                return RedirectToAction("Payment");
            }

            return View("Form", model);
        }


        // Show the Payment Page
        public IActionResult Payment()
        {
            // Get booking ID and total price from TempData
            if (TempData["BookingId"] == null || TempData["TotalPriceString"] == null)
            {
                return RedirectToAction("Index");
            }

            int bookingId = Convert.ToInt32(TempData["BookingId"]);
            decimal totalPrice = decimal.Parse(TempData["TotalPriceString"].ToString());

            // Preserve TempData for potential redirection
            TempData.Keep("BookingId");
            TempData.Keep("TotalPriceString");

            var model = new Payment
            {
                BookingId = bookingId,
                AmountDue = totalPrice
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ProcessPayment(Payment model)
        {
            if (ModelState.IsValid)
            {
                // Validate payment details based on payment method
                if ((model.PaymentMethod == "Credit Card" || model.PaymentMethod == "Debit Card") && string.IsNullOrWhiteSpace(model.CardNumber))
                {
                    ModelState.AddModelError("CardNumber", "Card number is required for credit/debit card payments.");
                    return View("Payment", model);
                }

                if ((model.PaymentMethod == "PayPal" || model.PaymentMethod == "GCash" || model.PaymentMethod == "Maya") && string.IsNullOrWhiteSpace(model.PhoneNumber))
                {
                    ModelState.AddModelError("PhoneNumber", "Phone number or email is required for this payment method.");
                    return View("Payment", model);
                }

                // Find the booking
                var booking = await _context.Bookings.FindAsync(model.BookingId);
                if (booking == null)
                {
                    TempData["ErrorMessage"] = "Booking not found.";
                    return RedirectToAction("Index");
                }

                // Generate a confirmation code
                booking.ConfirmationCode = GenerateConfirmationCode();
                booking.Status = BookingStatus.Confirmed;

                // Update room inventory for each day of the booking
                for (DateTime date = booking.CheckInDate; date < booking.CheckOutDate; date = date.AddDays(1))
                {
                    // Try to find existing inventory
                    var inventory = await _context.RoomInventories
                        .FirstOrDefaultAsync(ri => ri.RoomId == booking.RoomId && ri.Date.Date == date.Date);

                    if (inventory != null)
                    {
                        // Ensure we don't go below zero
                        inventory.AvailableRooms = Math.Max(0, inventory.AvailableRooms - 1);
                    }
                    else
                    {
                        // Create new inventory record with default values
                        _context.RoomInventories.Add(new RoomInventory
                        {
                            RoomId = booking.RoomId,
                            Date = date,
                            TotalRooms = 10,
                            AvailableRooms = 9  // Start with 9 as 1 is being booked
                        });
                    }
                }

                await _context.SaveChangesAsync();

                // Store confirmation code for the confirmation page
                TempData["ConfirmationCode"] = booking.ConfirmationCode;
                TempData["PaymentSuccess"] = "Payment processed successfully!";

                return RedirectToAction("ConfirmPay");
            }

            return View("Payment", model);
        }

        // Helper method to generate a random confirmation code
        private string GenerateConfirmationCode()
        {
            // Generate a code with format HE-XXXX-XXXX
            Random random = new Random();
            string numbers = string.Join("", Enumerable.Range(0, 8).Select(_ => random.Next(0, 10)));
            return $"HE-{numbers.Substring(0, 4)}-{numbers.Substring(4, 4)}";
        }

        // Show Payment Confirmation Page
        public IActionResult ConfirmPay()
        {
            // Get confirmation code from TempData
            string? confirmationCode = TempData["ConfirmationCode"]?.ToString();
            if (string.IsNullOrEmpty(confirmationCode))
            {
                // If there's no confirmation code, redirect to home
                return RedirectToAction("Index");
            }

            var model = new ConfirmPayment
            {
                ConfirmationCode = confirmationCode
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> VerifyPayment(ConfirmPayment model)
        {
            if (ModelState.IsValid)
            {
                // In a real application, this would verify the confirmation code
                // For our example, we'll just display a success message

                TempData["SuccessMessage"] = "Booking confirmed successfully! Thank you for choosing HotelEase.";
                return RedirectToAction("Index");
            }

            return View("ConfirmPay", model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}