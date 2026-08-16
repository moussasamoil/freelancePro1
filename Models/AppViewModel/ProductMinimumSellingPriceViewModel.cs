using System.ComponentModel.DataAnnotations;
using lotus_blue.Models;

namespace lotus_blue.ViewModels
{
    public class ProductMinimumSellingPriceViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يرجى اختيار البلد")]
        [Display(Name = "البلد")]
        public Common.Countries? Country { get; set; }

        [Required(ErrorMessage = "يرجى اختيار المتجر")]
        [Range(1, int.MaxValue, ErrorMessage = "يرجى اختيار المتجر")]
        [Display(Name = "المتجر")]
        public int? ManufacturingCompanyId { get; set; }

        [Required(ErrorMessage = "يرجى اختيار المنتج الرئيسي")]
        [Range(1, int.MaxValue, ErrorMessage = "يرجى اختيار المنتج الرئيسي")]
        [Display(Name = "المنتج الرئيسي")]
        public int? MainWarehouseId { get; set; }

        [Required(ErrorMessage = "يرجى إدخال الحد الأدنى للبيع")]
        [Range(0.01, 999999999, ErrorMessage = "الحد الأدنى للبيع يجب أن يكون أكبر من صفر")]
        [Display(Name = "الحد الأدنى للبيع")]
        public decimal? MinimumSellingPrice { get; set; }
    }
}
