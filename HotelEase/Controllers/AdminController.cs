using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using HotelEase.Data;
using HotelEase.Models;
using System.Linq;
using System.Threading.Tasks;

namespace HotelEase.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/RoomInventory
        public async Task<IActionResult> RoomInventory()
        {
            var user = await _context.Users.FindAsync(User.Identity.Name);
            if (user == null || !user.IsAdmin)
            {
                return RedirectToAction("Index", "Home");
            }

            var rooms = await _context.Rooms.ToListAsync();
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
    }
}