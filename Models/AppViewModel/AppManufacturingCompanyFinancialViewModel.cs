namespace lotus_blue.Models.AppViewModel
{
    public class AppManufacturingCompanyFinancialViewModel
    {
        public string ManufacturingCompanyName { get; set; }

        public string ManufacturingCompanyLogo { get; set; }

        public string TotalPriceTL { get; set; }
        public string TotalPriceUSD { get; set; }


    }
    public class CombinedTotalViewModel
    {
        public string TotalPriceTLCombined { get; set; } // Total price in TL for all companies combined
        public string TotalPriceUSDCombined { get; set; } // Total price in USD for all companies combined
    }
}
