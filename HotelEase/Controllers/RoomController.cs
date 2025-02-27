using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotelEase.Data;
using HotelEase.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HotelEase.Controllers
{
    public class RoomController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RoomController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> RCategory()
        {
            var rooms = await _context.Rooms
                .OrderBy(r => r.Category)
                .ToListAsync();

            var categories = rooms.Select(r => r.Category).Distinct().ToList();
            var roomsByCategory = new Dictionary<string, List<Room>>();

            foreach (var category in categories)
            {
                roomsByCategory[category] = rooms.Where(r => r.Category == category).ToList();
            }

            return View(roomsByCategory);
        }

        public async Task<IActionResult> RList(string category)
        {
            var rooms = await _context.Rooms
                .Where(r => r.Category == category)
                .ToListAsync();

            return View(rooms);
        }

        public async Task<IActionResult> RDetails(int id, DateTime? date = null)
        {
            var selectedDate = date ?? DateTime.Today;

            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound();
            }

            // Get availability for the selected date
            var availability = await _context.RoomInventories
                .FirstOrDefaultAsync(ri => ri.RoomId == id && ri.Date.Date == selectedDate.Date);

            var viewModel = new RoomDetailsViewModel
            {
                Room = room,
                SelectedDate = selectedDate,
                Availability = availability
            };

            return View(viewModel);
        }

        public IActionResult Form(int roomId, DateTime checkInDate, DateTime checkOutDate)
        {
            // Pre-populate the form with selected room and dates
            var model = new FormModel
            {
                RoomId = roomId,
                CheckInDate = checkInDate,
                CheckOutDate = checkOutDate
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> CheckAvailability(int roomId, DateTime checkInDate, DateTime checkOutDate)
        {
            // Check if selected dates are valid
            if (checkInDate < DateTime.Today)
            {
                TempData["ErrorMessage"] = "Check-in date cannot be in the past.";
                return RedirectToAction("RDetails", new { id = roomId });
            }

            if (checkOutDate <= checkInDate)
            {
                TempData["ErrorMessage"] = "Check-out date must be after check-in date.";
                return RedirectToAction("RDetails", new { id = roomId });
            }

            // Check room availability for each day in the range
            bool isAvailable = true;
            for (DateTime date = checkInDate; date < checkOutDate; date = date.AddDays(1))
            {
                var inventory = await _context.RoomInventories
                    .FirstOrDefaultAsync(ri => ri.RoomId == roomId && ri.Date.Date == date.Date);

                if (inventory == null || inventory.AvailableRooms <= 0)
                {
                    isAvailable = false;
                    break;
                }
            }

            if (!isAvailable)
            {
                TempData["ErrorMessage"] = "Room is not available for the selected dates.";
                return RedirectToAction("RDetails", new { id = roomId });
            }

            // If available, redirect to the booking form
            return RedirectToAction("Form", "Home", new
            {
                roomId = roomId,
                checkInDate = checkInDate.ToString("yyyy-MM-dd"),
                checkOutDate = checkOutDate.ToString("yyyy-MM-dd")
            });
        }

        public class RoomDetailsViewModel
        {
            public Room Room { get; set; }
            public DateTime SelectedDate { get; set; }
            public RoomInventory Availability { get; set; }
        }
    }
}