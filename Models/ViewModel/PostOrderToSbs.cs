namespace lotus_blue.Models.ViewModel
{
    public class PostOrderToSbs
    {
        public int id { get; set; }
        public int Country { get; set; }
        public string State { get; set; }
        public int OrderSource { get; set; }
        public string SourceName { get; set; }
        public int? StoreId { get; set; }
        public string TelephoneNumber { get; set; }
        public string SecondTelephoneNumber { get; set; }
        public string CustomerName { get; set; }
        public string Notes { get; set; }
        public string Address { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
