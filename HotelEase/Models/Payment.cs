using System;
using System.ComponentModel.DataAnnotations;


namespace HotelEase.Models
{
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        [Required(ErrorMessage = "Please select a payment method.")]
        public string PaymentMethod { get; set; }

        [StringLength(16, MinimumLength = 13, ErrorMessage = "Card number must be between 13 and 16 digits.")]
        public string? CardNumber { get; set; }

        [StringLength(50, ErrorMessage = "Phone number or email must not exceed 50 characters.")]
        public string? PhoneNumber { get; set; }

        [Required]
        [Range(1, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal AmountDue { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; } = DateTime.Now;
    }
}

