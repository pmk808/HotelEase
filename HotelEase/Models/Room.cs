namespace HotelEase.Models
{
    public class Room
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; } // Standard, Deluxe, Suite
        public string BedType { get; set; } // Single, Twin, Queen, King
        public decimal Price { get; set; }
    }

}
