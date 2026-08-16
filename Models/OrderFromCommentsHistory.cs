namespace lotus_blue.Models
{
    public class OrderFromCommentsHistory
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public bool PreviousValue { get; set; }
        public bool NewValue { get; set; }
        public DateTime EditedDate { get; set; }

        // Navigation property to the user who made the change
        public string ApplicationUserId { get; set; }
        public ApplicationUser User { get; set; }
    }
}
