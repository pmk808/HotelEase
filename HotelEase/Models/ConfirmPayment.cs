using System.ComponentModel.DataAnnotations;

namespace HotelEase.Models
{
    public class ConfirmPayment
    {
        [Required(ErrorMessage = "Confirmation code is required.")]
        [Display(Name = "Confirmation Code")]
        public string ConfirmationCode { get; set; }
    }
}
