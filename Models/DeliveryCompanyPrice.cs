using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models
{
    public class DeliveryCompanyPrice
    {
        public int Id { get; set; }

        [Display(Name = "البلد المحدد")]
        [Required(ErrorMessage = "يجب تحديد البلد")]
        public Common.Countries Country { get; set; }


        [Display(Name = "السعر")]
        [Required(ErrorMessage = "يجب تحديد السعر")]
        public decimal Price { get; set; }

        [Display(Name = "المدينة")]
        public string? City { get; set; }

        // Foreign key to DeliveryCompany
        [Required(ErrorMessage = "يجب تحديد الشركة")]
        public int DeliveryCompanyId { get; set; }
        public DeliveryCompany DeliveryCompany { get; set; } // Navigation property to DeliveryCompany
    }
}
