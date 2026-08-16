using Microsoft.AspNetCore.Mvc.Rendering;

namespace lotus_blue.Models.ViewModel
{
    public class OrderBySocialMediaCampaignsViewModel
    {
      
        public Common.Countries Country { get; set; }
        public OrderSourceEnum OrderSource { get; set; }
        public decimal TotalPrice { get; set; }
        public string? State { get; set; }
    }
}
