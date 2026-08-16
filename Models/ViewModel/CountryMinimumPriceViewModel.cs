using System.ComponentModel.DataAnnotations;
using static lotus_blue.Models.Common;

namespace lotus_blue.ViewModels
{
    public class CountryMinimumPriceViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "البلد")]
        public Countries Country { get; set; }

        [Display(Name = "المتجر")]
        public int? ManufacturingCompanyId { get; set; }

        public string? ManufacturingCompanyName { get; set; }

        [Required]
        [Display(Name = "الحد الأدنى للسعر بل تخفيضات")]
        public decimal MinimumPriceForOffers { get; set; }

        [Display(Name = "الحد الأقصى للسعر بل تخفيضات")]
        public decimal? MaximumPriceForOffers { get; set; }
    }
}
