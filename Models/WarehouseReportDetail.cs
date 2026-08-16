namespace lotus_blue.Models
{
    public class WarehouseReportDetail
    {
        public int Id { get; set; }
        public int WarehouseReportId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Amount { get; set; }

        // Navigation property
        public WarehouseReport WarehouseReport { get; set; }
    }
}
