using static lotus_blue.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lotus_blue.Models
{
    public class CountryMinimumPrice
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "البلد")]
        public Countries Country { get; set; }

        [Display(Name = "المتجر")]
        public int? ManufacturingCompanyId { get; set; }

        [ForeignKey(nameof(ManufacturingCompanyId))]
        public ManufacturingCompany? ManufacturingCompany { get; set; }

        [Required]
        [Display(Name = "  الحد الأدنى للسعر بل تخفيضات")]
        public decimal MinimumPriceForOffers { get; set; }

        [Display(Name = "  الحد الأقصى للسعر بل تخفيضات")]
        public decimal? MaximumPriceForOffers { get; set; }  // Optional upper bound
    }

}
