using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using HotelEase.Data;
using HotelEase.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace HotelEase.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(
           ApplicationDbContext context,
           UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Admin/RoomInventory
        public async Task<IActionResult> RoomInventory()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.IsAdmin)
            {
                return RedirectToAction("Index", "Home");
            }

            var rooms = await _context.Rooms.ToListAsync();

            // Get today's availability for each room
            var today = DateTime.Today;
            var roomInventories = await _context.RoomInventories
                .Where(ri => ri.Date.Date == today.Date)
                .ToListAsync();

            // Create a dictionary of roomId -> availableRooms
            var availabilityData = new Dictionary<int, int>();
            foreach (var inventory in roomInventories)
            {
                availabilityData[inventory.RoomId] = inventory.AvailableRooms;
            }

            // For rooms with no inventory record today, use total rooms as available
            foreach (var room in rooms)
            {
                if (!availabilityData.ContainsKey(room.Id))
                {
                    // Get the most recent inventory for this room
                    var lastInventory = await _context.RoomInventories
                        .Where(ri => ri.RoomId == room.Id)
                        .OrderByDescending(ri => ri.Date)
                        .FirstOrDefaultAsync();

                    // Use last inventory or default to 10
                    availabilityData[room.Id] = lastInventory?.TotalRooms ?? 10;
                }
            }

            // Pass availability data to the view
            ViewBag.AvailabilityData = availabilityData;

            return View(rooms);
        }

        // GET: Admin/CreateRoom
        public IActionResult CreateRoom()
        {
            return View();
        }

        // POST: Admin/CreateRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRoom(Room room)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.IsAdmin)
            {
                return RedirectToAction("Index", "Home");
            }

            if (ModelState.IsValid)
            {
                _context.Add(room);
                await _context.SaveChangesAsync();

                // After creating a new room, initialize its inventory for the next 30 days
                var today = DateTime.Today;
                for (int i = 0; i < 30; i++)
                {
                    var date = today.AddDays(i);
                    _context.RoomInventories.Add(new RoomInventory
                    {
                        RoomId = room.Id,
                        Date = date,
                        TotalRooms = 10,
                        AvailableRooms = 10
                    });
                }
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(RoomInventory));
            }
            return View(room);
        }

        // GET: Admin/UpdateInventory/5
        public async Task<IActionResult> UpdateInventory(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.IsAdmin)
            {
                return RedirectToAction("Index", "Home");
            }

            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound();
            }

            // Find the most recent inventory for this room or create a default one
            var today = DateTime.Today;
            var inventory = await _context.RoomInventories
                .FirstOrDefaultAsync(ri => ri.RoomId == id && ri.Date.Date == today.Date);

            var viewModel = new RoomInventoryViewModel
            {
                RoomId = room.Id,
                RoomName = room.Name,
                TotalRooms = inventory?.TotalRooms ?? 10,
                AvailableRooms = inventory?.AvailableRooms ?? 10,
                Date = today
            };

            return View(viewModel);
        }

        // POST: Admin/UpdateInventory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateInventory(RoomInventoryViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.IsAdmin)
            {
                return RedirectToAction("Index", "Home");
            }

            if (ModelState.IsValid)
            {
                // Validate that available rooms don't exceed total rooms
                if (model.AvailableRooms > model.TotalRooms)
                {
                    ModelState.AddModelError("AvailableRooms", "Available rooms cannot exceed total rooms.");
                    return View(model);
                }

                // Check if an inventory entry exists for this room and date
                var existingInventory = await _context.RoomInventories
                    .FirstOrDefaultAsync(ri => ri.RoomId == model.RoomId && ri.Date.Date == model.Date.Date);

                if (existingInventory != null)
                {
                    // Update existing inventory
                    existingInventory.TotalRooms = model.TotalRooms;
                    existingInventory.AvailableRooms = model.AvailableRooms;
                }
                else
                {
                    // Create new inventory entry
                    var inventory = new RoomInventory
                    {
                        RoomId = model.RoomId,
                        TotalRooms = model.TotalRooms,
                        AvailableRooms = model.AvailableRooms,
                        Date = model.Date
                    };
                    _context.Add(inventory);
                }

                await _context.SaveChangesAsync();

                // Show success message
                TempData["SuccessMessage"] = "Room inventory updated successfully.";

                return RedirectToAction(nameof(RoomInventory));
            }
            return View(model);
        }

        // GET: Admin/ViewInventoryCalendar/5
        public async Task<IActionResult> ViewInventoryCalendar(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.IsAdmin)
            {
                return RedirectToAction("Index", "Home");
            }

            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound();
            }

            // Get 30-day inventory for this room
            var today = DateTime.Today;
            var endDate = today.AddDays(30);

            var inventories = await _context.RoomInventories
                .Where(ri => ri.RoomId == id && ri.Date >= today && ri.Date < endDate)
                .OrderBy(ri => ri.Date)
                .ToListAsync();

            // Create view model with room details and inventory data
            var viewModel = new RoomInventoryCalendarViewModel
            {
                Room = room,
                Inventories = inventories,
                StartDate = today,
                EndDate = endDate
            };

            return View(viewModel);
        }

        // View Model for the inventory calendar
        public class RoomInventoryCalendarViewModel
        {
            public Room Room { get; set; }
            public List<RoomInventory> Inventories { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }

        // View Model for updating room inventory
        public class RoomInventoryViewModel
        {
            public int RoomId { get; set; }
            public string RoomName { get; set; }
            public int TotalRooms { get; set; }
            public int AvailableRooms { get; set; }
            public DateTime Date { get; set; }
        }

        // GET: Admin/EditRoom/5
        public async Task<IActionResult> EditRoom(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.IsAdmin)
            {
                return RedirectToAction("Index", "Home");
            }

            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound();
            }

            return View(room);
        }

        // POST: Admin/EditRoom/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRoom(int id, Room room)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.IsAdmin)
            {
                return RedirectToAction("Index", "Home");
            }

            if (id != room.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(room);
                    await _context.SaveChangesAsync();

                    // Success message
                    TempData["SuccessMessage"] = "Room updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RoomExists(room.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(RoomInventory));
            }
            return View(room);
        }

        // GET: Admin/DeleteRoom/5
        public async Task<IActionResult> DeleteRoom(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.IsAdmin)
            {
                return RedirectToAction("Index", "Home");
            }

            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound();
            }

            // Check if there are any active bookings for this room
            var hasActiveBookings = await _context.Bookings
                .AnyAsync(b => b.RoomId == id &&
                       (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) &&
                       b.CheckOutDate > DateTime.Today);

            if (hasActiveBookings)
            {
                TempData["ErrorMessage"] = "Cannot delete room with active bookings.";
                return RedirectToAction(nameof(RoomInventory));
            }

            return View(room);
        }

        // POST: Admin/DeleteRoom/5
        [HttpPost, ActionName("DeleteRoom")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoomConfirmed(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.IsAdmin)
            {
                return RedirectToAction("Index", "Home");
            }

            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound();
            }

            // Check if there are any active bookings for this room
            var hasActiveBookings = await _context.Bookings
                .AnyAsync(b => b.RoomId == id &&
                       (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) &&
                       b.CheckOutDate > DateTime.Today);

            if (hasActiveBookings)
            {
                TempData["ErrorMessage"] = "Cannot delete room with active bookings.";
                return RedirectToAction(nameof(RoomInventory));
            }

            // Delete all inventory records for this room
            var inventories = await _context.RoomInventories
                .Where(ri => ri.RoomId == id)
                .ToListAsync();

            _context.RoomInventories.RemoveRange(inventories);

            // Delete the room
            _context.Rooms.Remove(room);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Room deleted successfully.";

            return RedirectToAction(nameof(RoomInventory));
        }

        private bool RoomExists(int id)
        {
            return _context.Rooms.Any(e => e.Id == id);
        }
    }
}