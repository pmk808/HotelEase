using Microsoft.AspNetCore.Identity;

namespace HotelEase.Models
{
    public class ApplicationUser : IdentityUser
    {
        public bool IsAdmin { get; set; } = false;
        public string FullName { get; set; }
    }
}