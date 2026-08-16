namespace lotus_blue.Models
{
    public class WarehouseReport
    {
        public int Id { get; set; } // This serves as the unique identifier for the report

        // Foreign key to DeliveryCompany
        public int DeliveryCompanyId { get; set; }
        public DeliveryCompany DeliveryCompany { get; set; } // Navigation property to DeliveryCompany

        public decimal TotalPriceOfAllProducts { get; set; }
        public DateTime CreatedDate { get; set; }

        // Properties for delivered and failed items
        public int TotalDeliveredItemsFromSpecificOrders { get; set; }
        public int TotalFailedDeliveredItemsFromSpecificOrders { get; set; }

        // Relationship to store detailed items
        public List<WarehouseReportDetail> Items { get; set; }
    }
}
