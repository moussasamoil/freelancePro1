namespace lotus_blue.Models
{
    public class OrderReportOrder
    {
        public int OrderReportId { get; set; }
        public OrderReport OrderReport { get; set; }

        public int OrderId { get; set; }
        public Order Order { get; set; }
    }

}
