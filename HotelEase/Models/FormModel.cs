﻿using System;
using System.ComponentModel.DataAnnotations;

namespace HotelEase.Models
{
    public class FormModel
    {
        [Required(ErrorMessage = "Full Name is required.")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; }

        [Required]
        public int RoomId { get; set; }

        [Required(ErrorMessage = "Check-in Date is required.")]
        [DataType(DataType.Date)]
        public DateTime CheckInDate { get; set; }

        [Required(ErrorMessage = "Check-out Date is required.")]
        [DataType(DataType.Date)]
        [DateAfter(nameof(CheckInDate), ErrorMessage = "Check-out date must be after check-in date.")]
        public DateTime CheckOutDate { get; set; }

        // Additional properties for display purposes (not saved to database)
        public string RoomName { get; set; }
        public string RoomCategory { get; set; }
        public string RoomBedType { get; set; }
        public decimal RoomPrice { get; set; }
        public int? BookingId { get; set; }
    }

    // Custom validation attribute to ensure check-out date is after check-in date
    public class DateAfterAttribute : ValidationAttribute
    {
        private readonly string _comparisonProperty;

        public DateAfterAttribute(string comparisonProperty)
        {
            _comparisonProperty = comparisonProperty;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var comparisonValue = validationContext.ObjectType.GetProperty(_comparisonProperty)
                ?.GetValue(validationContext.ObjectInstance) as DateTime?;

            if (value is DateTime currentValue && comparisonValue.HasValue)
            {
                if (currentValue <= comparisonValue.Value)
                {
                    return new ValidationResult(ErrorMessage);
                }
            }

            return ValidationResult.Success;
        }
    }
}
