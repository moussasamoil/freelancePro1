namespace lotus_blue.Models.ViewModel
{
    public class OrderReportViewModel
    {
        public int Id { get; set; }
        public string GeneratedTime { get; set; }
        public string TotalAmount { get; set; }
        public string Country { get; set; }
        public string? DeliveryCompanyName { get; set; }
        public string? StoreName { get; set; }
        public string Currency { get; set; }

        public string City { get; set; }
    }

}
