using Microsoft.AspNetCore.Mvc.Rendering;
using static lotus_blue.Models.Common;

namespace lotus_blue.Models.AppViewModel
{
    public class AppManufacturingCompanyOrderDetailsViewModel
    {
        public int? CompanyId { get; set; }

        public string CompanyName { get; set; }

        public string companyimage { get; set; }
        public List<AppCountryOrderInfo> CountriesOrderInfo { get; set; }
        public int TotalOrders { get; set; } // Add this property


        public string TotalPriceUSD { get; set; }

        public string TotalPriceTRY { get; set; }


    }


    public class AppCountryOrderInfo
    {
        public Countries Country { get; set; }

        public string Currency { get; set; }
        public int TotalOrders { get; set; }
        public Dictionary<int, string> TotalPriceLocalCurrency { get; set; } // New property

        public Dictionary<int, int> OrdersBySource { get; set; } // Changed property type

        public Dictionary<int, string> TotalPriceBySourceTRY { get; set; } // Changed property type

        public Dictionary<int, string> TotalPriceBySourceUSD { get; set; } // Changed property type

        public int TotalOrdersCount { get; set; }
        public string TotalLocalCurrencyPriceSum { get; set; }
        public string TotalTryPriceSum { get; set; }
        public string TotalUsdPriceSum { get; set; }
    }
}


