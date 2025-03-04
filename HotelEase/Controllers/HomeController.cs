using System.Diagnostics;
using HotelEase.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotelEase.Data;
using Stripe;
using Microsoft.Extensions.Options;

namespace HotelEase.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOptions<StripeSettings> _stripeSettings;

        public HomeController(
        ILogger<HomeController> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IOptions<StripeSettings> stripeSettings)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _stripeSettings = stripeSettings;
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

                // Check if this is an update to an existing booking
                if (model.BookingId.HasValue && model.BookingId.Value > 0)
                {
                    // Get the existing booking
                    var existingBooking = await _context.Bookings.FindAsync(model.BookingId.Value);
                    if (existingBooking != null)
                    {
                        // Verify the current user owns this booking
                        var user = await _userManager.GetUserAsync(User);
                        if (user == null || (existingBooking.UserId != user.Id && !user.IsAdmin))
                        {
                            return Unauthorized();
                        }

                        // Only allow editing of pending bookings
                        if (existingBooking.Status != BookingStatus.Pending)
                        {
                            TempData["ErrorMessage"] = "Only pending bookings can be edited.";
                            return RedirectToAction("UserDashboard", "Dashboard");
                        }

                        // Update booking details
                        existingBooking.CheckInDate = model.CheckInDate;
                        existingBooking.CheckOutDate = model.CheckOutDate;
                        existingBooking.TotalPrice = totalPrice;

                        await _context.SaveChangesAsync();

                        // Store the booking ID in TempData for the payment page
                        TempData["BookingId"] = existingBooking.Id;
                        TempData["TotalPriceString"] = totalPrice.ToString();

                        TempData["SuccessMessage"] = "Booking updated successfully!";

                        return RedirectToAction("Payment");
                    }
                }

                // Create new booking in pending status
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

                try
                {
                    // Create a Stripe PaymentIntent
                    var options = new PaymentIntentCreateOptions
                    {
                        Amount = (long)(booking.TotalPrice * 100), // Stripe uses cents
                        Currency = "usd",
                        Description = $"HotelEase Booking #{booking.Id}",
                        PaymentMethodTypes = new List<string> { "card" },
                        Metadata = new Dictionary<string, string>
                {
                    { "BookingId", booking.Id.ToString() },
                    { "RoomId", booking.RoomId.ToString() },
                    { "CheckInDate", booking.CheckInDate.ToString("yyyy-MM-dd") },
                    { "CheckOutDate", booking.CheckOutDate.ToString("yyyy-MM-dd") }
                }
                    };

                    var service = new PaymentIntentService();
                    var paymentIntent = await service.CreateAsync(options);

                    // Only update booking to Confirmed status after successful payment
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
                    TempData["PaymentIntent"] = paymentIntent.ClientSecret;  // Used by Stripe.js
                    TempData["PaymentSuccess"] = "Payment processed successfully!";

                    return RedirectToAction("ConfirmPay");
                }
                catch (Stripe.StripeException e)
                {
                    ModelState.AddModelError(string.Empty, $"Payment error: {e.Message}");
                    return View("Payment", model);
                }
                catch (Exception e)
                {
                    ModelState.AddModelError(string.Empty, $"An error occurred: {e.Message}");
                    return View("Payment", model);
                }
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
        // GET: Edit Booking
        public async Task<IActionResult> EditBooking(int id)
        {
            // Get the booking
            var booking = await _context.Bookings
                .Include(b => b.Room)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound();
            }

            // Verify the current user owns this booking
            var user = await _userManager.GetUserAsync(User);
            if (user == null || (booking.UserId != user.Id && !user.IsAdmin))
            {
                return Unauthorized();
            }

            // Only allow editing of pending bookings
            if (booking.Status != BookingStatus.Pending)
            {
                TempData["ErrorMessage"] = "Only pending bookings can be edited.";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            // Create a form model with the booking details
            var model = new FormModel
            {
                BookingId = booking.Id,
                RoomId = booking.RoomId,
                RoomName = booking.Room.Name,
                RoomCategory = booking.Room.Category,
                RoomBedType = booking.Room.BedType,
                RoomPrice = booking.Room.Price,
                CheckInDate = booking.CheckInDate,
                CheckOutDate = booking.CheckOutDate,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty
            };

            ViewBag.IsEditing = true;

            return View("Form", model);
        }
        // POST: Update Booking
        [HttpPost]
        public async Task<IActionResult> UpdateBooking(FormModel model)
        {
            if (ModelState.IsValid)
            {
                // Get the existing booking
                var booking = await _context.Bookings.FindAsync(model.BookingId);
                if (booking == null)
                {
                    return NotFound();
                }

                // Verify the current user owns this booking
                var user = await _userManager.GetUserAsync(User);
                if (user == null || (booking.UserId != user.Id && !user.IsAdmin))
                {
                    return Unauthorized();
                }

                // Only allow editing of pending bookings
                if (booking.Status != BookingStatus.Pending)
                {
                    TempData["ErrorMessage"] = "Only pending bookings can be edited.";
                    return RedirectToAction("UserDashboard", "Dashboard");
                }

                // Update booking details
                booking.CheckInDate = model.CheckInDate;
                booking.CheckOutDate = model.CheckOutDate;

                // Calculate new total price
                int nights = (int)(model.CheckOutDate - model.CheckInDate).TotalDays;
                var room = await _context.Rooms.FindAsync(booking.RoomId);
                if (room == null)
                {
                    return NotFound();
                }
                booking.TotalPrice = room.Price * nights;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Booking updated successfully!";

                // Redirect to resume payment
                return RedirectToAction("ResumePayment", new { id = booking.Id });
            }

            return View("Form", model);
        }

        // GET: Resume Payment
        public async Task<IActionResult> ResumePayment(int id)
        {
            // Get the booking
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            // Verify the current user owns this booking
            var user = await _userManager.GetUserAsync(User);
            if (user == null || (booking.UserId != user.Id && !user.IsAdmin))
            {
                return Unauthorized();
            }

            // Only allow resuming payment for pending bookings
            if (booking.Status != BookingStatus.Pending)
            {
                TempData["ErrorMessage"] = "Payment can only be resumed for pending bookings.";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            // Store booking ID in TempData for the payment page
            TempData["BookingId"] = booking.Id;
            TempData["TotalPriceString"] = booking.TotalPrice.ToString();

            // Redirect to payment page
            return RedirectToAction("Payment");
        }

        // GET: GetAvailableRooms
        public async Task<IActionResult> GetAvailableRooms(string category, string bedType)
        {
            // Query for rooms that match the criteria
            var rooms = await _context.Rooms
                .Where(r => r.Category == category && r.BedType == bedType)
                .Select(r => new
                {
                    id = r.Id,
                    name = r.Name,
                    category = r.Category,
                    bedType = r.BedType,
                    price = r.Price
                })
                .ToListAsync();

            return Json(rooms);
        }

        // GET: GetBookingDetails
        public async Task<IActionResult> GetBookingDetails(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.Room)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound();
            }

            // Verify the current user owns this booking
            var user = await _userManager.GetUserAsync(User);
            if (user == null || (booking.UserId != user.Id && !user.IsAdmin))
            {
                return Unauthorized();
            }

            // Return a partial view with the booking details
            return PartialView("_BookingDetails", booking);
        }
    }
}