using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using static lotus_blue.Models.Common;

namespace lotus_blue.Models.ViewModel
{
    public class SalesIndicatorViewModel : IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اختاري الدولة")]
        [Display(Name = "الدولة")]
        public Countries? Country { get; set; }

        [Required(ErrorMessage = "اختاري المنتج الرئيسي")]
        [Display(Name = "المنتج الرئيسي")]
        public int MainWarehouseId { get; set; }

        [Required(ErrorMessage = "ادخلي من للحد الأدنى للبيع")]
        [Range(0, 999999999, ErrorMessage = "القيمة لا يمكن أن تكون أقل من صفر")]
        [Display(Name = "الحد الأدنى للبيع من")]
        public decimal MinimumSellingFrom { get; set; }

        [Required(ErrorMessage = "ادخلي إلى للحد الأدنى للبيع")]
        [Range(0, 999999999, ErrorMessage = "القيمة لا يمكن أن تكون أقل من صفر")]
        [Display(Name = "الحد الأدنى للبيع إلى")]
        public decimal MinimumSellingTo { get; set; }

        [Required(ErrorMessage = "ادخلي من للحد الأساسي للبيع")]
        [Range(0, 999999999, ErrorMessage = "القيمة لا يمكن أن تكون أقل من صفر")]
        [Display(Name = "الحد الأساسي للبيع من")]
        public decimal BasicSellingFrom { get; set; }

        [Required(ErrorMessage = "ادخلي إلى للحد الأساسي للبيع")]
        [Range(0, 999999999, ErrorMessage = "القيمة لا يمكن أن تكون أقل من صفر")]
        [Display(Name = "الحد الأساسي للبيع إلى")]
        public decimal BasicSellingTo { get; set; }

        [Required(ErrorMessage = "ادخلي من للحد الوسطي للبيع")]
        [Range(0, 999999999, ErrorMessage = "القيمة لا يمكن أن تكون أقل من صفر")]
        [Display(Name = "الحد الوسطي للبيع من")]
        public decimal MiddleSellingFrom { get; set; }

        [Required(ErrorMessage = "ادخلي إلى للحد الوسطي للبيع")]
        [Range(0, 999999999, ErrorMessage = "القيمة لا يمكن أن تكون أقل من صفر")]
        [Display(Name = "الحد الوسطي للبيع إلى")]
        public decimal MiddleSellingTo { get; set; }

        public List<SelectListItem> MainWarehouseList { get; set; } = new List<SelectListItem>();
        public List<SalesIndicatorCountryOptionViewModel> CountryOptions { get; set; } = new List<SalesIndicatorCountryOptionViewModel>();
        public List<SalesIndicatorProductOptionViewModel> MainWarehouseOptions { get; set; } = new List<SalesIndicatorProductOptionViewModel>();
        public List<SalesIndicatorRowViewModel> Rows { get; set; } = new List<SalesIndicatorRowViewModel>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Country.HasValue)
            {
                yield return new ValidationResult("اختاري الدولة", new[] { nameof(Country) });
            }

            if (MinimumSellingFrom > MinimumSellingTo)
            {
                yield return new ValidationResult("الحد الأدنى للبيع: قيمة من لا يمكن أن تكون أكبر من إلى", new[] { nameof(MinimumSellingFrom), nameof(MinimumSellingTo) });
            }

            if (BasicSellingFrom > BasicSellingTo)
            {
                yield return new ValidationResult("الحد الأساسي للبيع: قيمة من لا يمكن أن تكون أكبر من إلى", new[] { nameof(BasicSellingFrom), nameof(BasicSellingTo) });
            }

            if (MiddleSellingFrom > MiddleSellingTo)
            {
                yield return new ValidationResult("الحد الوسطي للبيع: قيمة من لا يمكن أن تكون أكبر من إلى", new[] { nameof(MiddleSellingFrom), nameof(MiddleSellingTo) });
            }
        }
    }

    public class SalesIndicatorCountryOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string ImageUrl { get; set; } = "static/earth-americas-sharp-solid.svg";
        public bool Selected { get; set; }
    }

    public class SalesIndicatorProductOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string ImageUrl { get; set; } = "static/DefaultImage.svg";
    }

    public class SalesIndicatorRowViewModel
    {
        public int Id { get; set; }
        public Countries Country { get; set; }
        public string CountryName { get; set; } = "";
        public string CountryImageUrl { get; set; } = "static/earth-americas-sharp-solid.svg";
        public int MainWarehouseId { get; set; }
        public string MainWarehouseName { get; set; } = "";
        public string MainWarehouseImageUrl { get; set; } = "static/DefaultImage.svg";
        public decimal MinimumSellingFrom { get; set; }
        public decimal MinimumSellingTo { get; set; }
        public decimal BasicSellingFrom { get; set; }
        public decimal BasicSellingTo { get; set; }
        public decimal MiddleSellingFrom { get; set; }
        public decimal MiddleSellingTo { get; set; }
    }
}