namespace lotus_blue.Models.ViewModel
{
    public class PrintOrdersForDeliveryViewModel
    {
        // Order report ID
        public int OrderReportId { get; set; }

        // First order's delivery company name
        public string FirstDeliveryCompanyName { get; set; }

        // Delivery company country
        public string DeliveryCompanyCountry { get; set; }

        // Today's date
        public DateTime TodayDate { get; set; }

        // Orders to be printed
        public List<OrderViewModel> Orders { get; set; }
    }

}
