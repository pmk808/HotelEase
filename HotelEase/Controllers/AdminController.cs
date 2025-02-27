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
            if (ModelState.IsValid)
            {
                _context.Add(room);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(RoomInventory));
            }
            return View(room);
        }

        // GET: Admin/UpdateInventory/5
        public async Task<IActionResult> UpdateInventory(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound();
            }

            // Create a view model for updating inventory
            var viewModel = new RoomInventoryViewModel
            {
                RoomId = room.Id,
                RoomName = room.Name,
                TotalRooms = 10, // Default value - you might have a different logic
                AvailableRooms = 10, // Default value
                Date = DateTime.Today
            };

            return View(viewModel);
        }

        // POST: Admin/UpdateInventory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateInventory(RoomInventoryViewModel model)
        {
            if (ModelState.IsValid)
            {
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
                return RedirectToAction(nameof(RoomInventory));
            }
            return View(model);
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
            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound();
            }

            return View(room);
        }

        // POST: Admin/DeleteRoom/5
        [HttpPost, ActionName("DeleteRoom")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoomConfirmed(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room != null)
            {
                _context.Rooms.Remove(room);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(RoomInventory));
        }

        private bool RoomExists(int id)
        {
            return _context.Rooms.Any(e => e.Id == id);
        }

        // GET: Admin/GetRoomAvailability
        public async Task<IActionResult> GetRoomAvailability(DateTime date)
        {
            // Default to today if no date provided
            if (date == default)
            {
                date = DateTime.Today;
            }

            // Get all rooms
            var rooms = await _context.Rooms.ToListAsync();

            // Get availability data for the specified date
            var inventoryData = await _context.RoomInventories
                .Where(ri => ri.Date.Date == date.Date)
                .ToListAsync();

            // Combine room and inventory data
            var availabilityData = new List<object>();

            foreach (var room in rooms)
            {
                var inventory = inventoryData.FirstOrDefault(ri => ri.RoomId == room.Id);

                // If no inventory record exists for this room/date, create default values
                int totalRooms = inventory?.TotalRooms ?? 10;
                int availableRooms = inventory?.AvailableRooms ?? 10;

                availabilityData.Add(new
                {
                    roomId = room.Id,
                    roomName = room.Name,
                    category = room.Category,
                    bedType = room.BedType,
                    totalRooms = totalRooms,
                    availableRooms = availableRooms,
                    date = date.ToString("yyyy-MM-dd")
                });
            }

            return Json(availabilityData);
        }

        // GET: Admin/UpdateRoomInventory
        public async Task<IActionResult> UpdateRoomInventory(int roomId, DateTime date)
        {
            var room = await _context.Rooms.FindAsync(roomId);
            if (room == null)
            {
                return NotFound();
            }

            // Find existing inventory or create a new one
            var inventory = await _context.RoomInventories
                .FirstOrDefaultAsync(ri => ri.RoomId == roomId && ri.Date.Date == date.Date);

            if (inventory == null)
            {
                inventory = new RoomInventory
                {
                    RoomId = roomId,
                    Date = date,
                    TotalRooms = 10,
                    AvailableRooms = 10
                };
            }

            var viewModel = new RoomInventoryViewModel
            {
                RoomId = roomId,
                RoomName = room.Name,
                Date = date,
                TotalRooms = inventory.TotalRooms,
                AvailableRooms = inventory.AvailableRooms
            };

            return View(viewModel);
        }
    }
}