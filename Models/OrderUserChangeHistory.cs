namespace lotus_blue.Models
{
    public class OrderUserChangeHistory
    {
        public int Id { get; set; } // Primary Key
        public int OrderId { get; set; } // Foreign Key to the Order
        public string PreviousOrderEntryUser { get; set; } // The user who previously entered the order
        public string NewOrderEntryUser { get; set; } // The user who is entering the order after the change
        public string ChangedBy { get; set; } // Username of the person who made the change
        public DateTime ChangedOn { get; set; } // Timestamp of when the change was made
    }
}
