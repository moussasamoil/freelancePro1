namespace lotus_blue.Models.ViewModel
{
    public class UpdateStatusRequest
    {
        public OrderStatusEnum NewStatus { get; set; }
        public string? Reason { get; set; } // Add this property to include the reason

    }

}
