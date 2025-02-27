namespace HotelEase.Models
{
    public class RoomInventory
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public Room Room { get; set; }
        public int TotalRooms { get; set; }
        public int AvailableRooms { get; set; }
        public DateTime Date { get; set; }

        // Helper property to determine availability status
        public AvailabilityStatus Status
        {
            get
            {
                if (AvailableRooms == 0)
                    return AvailabilityStatus.NotAvailable;
                else if ((double)AvailableRooms / TotalRooms < 0.3)
                    return AvailabilityStatus.LimitedAvailability;
                else
                    return AvailabilityStatus.Available;
            }
        }
    }

    public enum AvailabilityStatus
    {
        Available,          // Green - plenty of rooms
        LimitedAvailability, // Orange - limited rooms
        NotAvailable        // Red - no rooms
    }
}