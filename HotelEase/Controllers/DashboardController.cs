using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace HotelEase.Controllers
{
    public class DashboardController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;

        public DashboardController(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }


        public IActionResult AdminDashboard()
        {
            return View();
        }
        public async Task<IActionResult> UserDashboard()
        {
          

            return View();
        }
    }
}


