namespace lotus_blue.Models.ViewModel
{
    public class FinancialManufacturingCompanyViewModel
    {
        public List<GetFinancialManufacturingCompanyDataOnGoingDeliveryCompany> ManufacturingCompanyData { get; set; }
        public string TotalPriceDollar { get; set; }
        public string ToTalPriceTl { get; set; }
        public string TotalLocalCurrenyPrice { get; set; }
        public int NumberOfOrders { get; set; }
        public string Currency { get; set; }
    }

}
