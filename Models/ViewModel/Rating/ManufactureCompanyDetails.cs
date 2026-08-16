using static lotus_blue.Models.Common;

namespace lotus_blue.Models.ViewModel.Rating
{
    public class ManufactureCompanyDetails
    {
    }

    public class ManufacturingOrderSummaryViewModel
    {
        public int TotalOrders { get; set; }
        public string FixedOrdersCount { get; set; }
        public string OrderFromOffersCount { get; set; }
        public string OrderFromOfferDiscountsCount { get; set; }
        public int NumberOfBonuses { get; set; }
        public string TotalProductsCount { get; set; }
        public string OrderFromCommentsCount { get; set; }
        public string OrderFromFemalesCount { get; set; }
        public string OrderFromMalesCount { get; set; }
        public decimal TotalPriceUSD { get; set; }
        public decimal TotalPriceTRY { get; set; }
        public List<ManufacturingCompanyOrderDetailsViewModel> ManufacturingCompanyOrders { get; set; }
    }

    public class ManufacturingCompanyOrderDetailsViewModel
    {
        public int? StoreId { get; set; }
        public string StoreName { get; set; }
        public string StoreImage { get; set; }
        public List<CountryOrderInfo> CountriesOrderInfo { get; set; }

        // Add summary data specific to each manufacturing company
        public int TotalOrders { get; set; }
        public string FixedOrdersCount { get; set; }
        public string OrderFromOffersCount { get; set; }
        public string OrderFromOfferDiscountsCount { get; set; }
        public int NumberOfBonuses { get; set; }
        public string TotalProductsCount { get; set; }
        public string OrderFromCommentsCount { get; set; }
        public string OrderFromFemalesCount { get; set; }
        public string OrderFromMalesCount { get; set; }
        public decimal TotalPriceUSD { get; set; }
        public decimal TotalPriceTRY { get; set; }



    }

    public class CountryOrderInfo
    {
        public int CountryId { get; set; }
        public Countries Country { get; set; }
        public int TotalOrders { get; set; }
        public string TotalLocalCurrencyPriceSum { get; set; }
        public string TotalTryPriceSum { get; set; }
        public string TotalUsdPriceSum { get; set; }
        public Dictionary<OrderSourceEnum, int> OrdersBySource { get; set; } = new Dictionary<OrderSourceEnum, int>();
        public Dictionary<OrderSourceEnum, string> TotalPriceLocalCurrency { get; set; } = new Dictionary<OrderSourceEnum, string>();
        public Dictionary<OrderSourceEnum, string> TotalPriceBySourceTRY { get; set; } = new Dictionary<OrderSourceEnum, string>();
        public Dictionary<OrderSourceEnum, string> TotalPriceBySourceUSD { get; set; } = new Dictionary<OrderSourceEnum, string>();

        // new addition 
        public string FixedOrdersCount { get; set; }
        public string OrderFromOffersCount { get; set; }
        public string OrderFromOfferDiscountsCount { get; set; }
        public int NumberOfBonuses { get; set; }
        public string TotalProductsCount { get; set; }
        public string OrderFromCommentsCount { get; set; }
        public string OrderFromFemalesCount { get; set; }
        public string OrderFromMalesCount { get; set; }

    }

}
