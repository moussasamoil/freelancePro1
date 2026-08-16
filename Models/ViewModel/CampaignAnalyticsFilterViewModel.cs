using lotus_blue.Models;
using static lotus_blue.Models.Common;

namespace lotus_blue.ViewModels
{
    public class CampaignAnalyticsFilterViewModel
    {
        public CampaignAnalyticsViewModel Analytics { get; set; }
        public List<Campaign> Campaigns { get; set; }
        public Countries? SelectedCountryId { get; set; }
        public int? SelectedCampaignId { get; set; }
        public string StartDay { get; set; }
        public string EndDay { get; set; }
    }

    public class CampaignAnalyticsViewModel
    {
        public int TotalOrders { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal TotalPriceTRY { get; set; }
        public decimal TotalPriceUSD { get; set; }
        public List<CampaignSalesViewModel> CampaignSales { get; set; } = new();


    }

    public class CampaignSalesViewModel
    {
        public int CampaignId { get; set; }
        public string CampaignName { get; set; }
        public string ImageUrl { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalSalesUSD { get; set; }
        public decimal TotalSalesTRY { get; set; }
    }

}