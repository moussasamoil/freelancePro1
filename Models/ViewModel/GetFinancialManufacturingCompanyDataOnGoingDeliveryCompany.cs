namespace lotus_blue.Models.ViewModel
{

    public class GetFinancialManufacturingCompanyDataOnGoingDeliveryCompany
    {
        public GetDataListViewModel ManufacturingCompany { get; set; }
        public List<FinancialDeliveryComapnyViewModelDataList> DeliveryCompanies { get; set; }

        public string TotalPriceDollar { get; set; }
        public string ToTalPriceTl { get; set; }
        public string TotalLocalCurrenyPrice { get; set; }
        public int NumberOfOrders { get; set; }
    }

    public class FinancialDeliveryComapnyViewModelDataList
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string LogoUrl { get; set; }
        public Common.Countries Country { get; set; }
        public string City { get; set; }
        public string Currency { get; set; }
        public string DeferredDeliveryCompanyPrice { get; set; }
        public string DeferredDifference { get; set; }

        public string OnGoingAccountDifference { get; set; }
        public string OnGoingAccountDeliveryCompanyPrice { get; set; }
    }




}
