using Microsoft.AspNetCore.Mvc.Rendering;
using static lotus_blue.Models.Common; // Ensure this is the correct location of your Enums

namespace lotus_blue.Models.ViewModel
{
    public class CountryOrderDetailsViewModel
    {
        public Countries Country { get; set; }
        public List<CityOrderInfo> CitiesOrderInfo { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalPriceUSD { get; set; }
        public string PercentageOfAllOrders { get; set; }     

    }

    public class CityOrderInfo
    {
        public string CityName { get; set; }
        public int TotalOrders { get; set; }
        public string PercentageOfTotalOrders { get; set; } // New property
        public Dictionary<OrderSourceEnum, int> OrdersBySource { get; set; }
        public Dictionary<OrderSourceEnum, decimal> TotalPriceBySourceUSD { get; set; }
    }
}
