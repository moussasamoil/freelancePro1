namespace lotus_blue.Models.ViewModel
{
    public class UpdateStatusViewModel
    {
        public List<int>? OrderIds { get; set; } // Add this property for order IDs

        public OrderStatusEnum NewStatus { get; set; }

        public string? Reason { get; set; }
      
    }
}
