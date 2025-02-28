using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using HotelEase.Models;
using HotelEase.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelEase.Controllers
{
    [Authorize] // Require login for all dashboard actions
    public class DashboardController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public DashboardController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            if (user.IsAdmin)
            {
                return RedirectToAction("AdminDashboard");
            }
            else
            {
                return RedirectToAction("UserDashboard");
            }
        }

        public async Task<IActionResult> AdminDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.IsAdmin)
            {
                return RedirectToAction("Index", "Home");
            }

            // For admin, get all bookings with related data
            var bookings = await _context.Bookings
                .Include(b => b.Room)
                .Include(b => b.User)
                .OrderByDescending(b => b.BookingDate) // Show newest first
                .ToListAsync();

            return View(bookings);
        }

        public async Task<IActionResult> UserDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            // Get user's bookings to display on the dashboard
            var bookings = await _context.Bookings
                .Where(b => b.UserId == user.Id)
                .Include(b => b.Room)
                .OrderByDescending(b => b.BookingDate) // Show newest first
                .ToListAsync();

            return View(bookings);
        }

        // Action methods for handling booking status changes

        [HttpPost]
        public async Task<IActionResult> ConfirmBooking(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.IsAdmin)
            {
                return Unauthorized();
            }

            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            booking.Status = BookingStatus.Confirmed;
            await _context.SaveChangesAsync();

            return RedirectToAction("AdminDashboard");
        }

        [HttpPost]
        public async Task<IActionResult> CancelBooking(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            // Only allow admins or the booking owner to cancel
            if (!user.IsAdmin && booking.UserId != user.Id)
            {
                return Unauthorized();
            }

            booking.Status = BookingStatus.Cancelled;

            // Restore room inventory when booking is cancelled
            for (DateTime date = booking.CheckInDate; date < booking.CheckOutDate; date = date.AddDays(1))
            {
                var inventory = await _context.RoomInventories
                    .FirstOrDefaultAsync(ri => ri.RoomId == booking.RoomId && ri.Date.Date == date.Date);

                if (inventory != null)
                {
                    // Add the room back to inventory
                    inventory.AvailableRooms += 1;
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(user.IsAdmin ? "AdminDashboard" : "UserDashboard");
        }

        [HttpPost]
        public async Task<IActionResult> CompleteBooking(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.IsAdmin)
            {
                return Unauthorized();
            }

            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            booking.Status = BookingStatus.Completed;
            await _context.SaveChangesAsync();

            return RedirectToAction("AdminDashboard");
        }
    }
}