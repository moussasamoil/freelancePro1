using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace lotus_blue.Models.ViewModel
{
    public class CitiesWithoutDeliveryPricesViewModel
    {
        public string? SelectedCountry { get; set; }
        public int? SelectedDeliveryCompanyId { get; set; }
        public List<SelectListItem> CountryList { get; set; } = new();
        public List<SelectListItem> DeliveryCompanyList { get; set; } = new();
        public List<CityWithoutDeliveryPriceRowViewModel> Rows { get; set; } = new();
    }

    public class CityWithoutDeliveryPriceRowViewModel
    {
        public int DeliveryCompanyId { get; set; }
        public string DeliveryCompanyName { get; set; } = string.Empty;
        public string? DeliveryCompanyLogo { get; set; }
        public string CountryName { get; set; } = string.Empty;
        public string CountryImageUrl { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
    }
}
