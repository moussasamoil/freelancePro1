using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models
{
    public class DeliveryCompany
    {
        public int Id { get; set; }

        [Display(Name = "لوغو الشركة ")]
        public string? ImageUrl { get; set; }

        [Display(Name = "معلومات الشركة")]
        public string? InformationUrl { get; set; }

        [Display(Name = "الاسم")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "يجب أن يكون الاسم بين 3 و 100 حرفًا.")]
        [Required]
        public string Name { get; set; }

        [DisplayName("اسم العرض")]
        [StringLength(100)]
        public string? DisplayName { get; set; }

        [Display(Name = "الرقم الضريبي")]
        [StringLength(20, ErrorMessage = "يجب أن يكون الرقم الضريبي أقل من 20 حرفًا.")]
        public string? TaxRegistrationNumber { get; set; }

        [Display(Name = "العنوان")]
        [Required]
        public string Address { get; set; }

        [Display(Name = "رقم الهوية")]
        [StringLength(20, ErrorMessage = "يجب أن يكون رقم الهوية أقل من 20 حرفًا.")]
        [Required]
        public string IdNumber { get; set; }

        [Display(Name = "رقم الهاتف")]
        [RegularExpression(@"^\+?[0-9]{8,15}$", ErrorMessage = "رقم الهاتف غير صالح.")]
        [Required]
        public string PhoneNumber { get; set; }

        [Display(Name = "التخصص")]
        public string? specialty { get; set; }

        [Display(Name = "الموقع الإلكتروني")]
        public string? Website { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(500, ErrorMessage = "يجب أن تكون الملاحظات أقل من 500 حرف.")]
        public string? Notes { get; set; }

        [Display(Name = "البلد المحدد")]
        [Required(ErrorMessage = "يجب تحديد البلد")]
        public Common.Countries Country { get; set; }

        [Display(Name = "المدينة")]
        public string? City { get; set; }

        [Display(Name = "تاريخ الأنشاء ")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; }

        [Required]
        public string UserId { get; set; } // Foreign key to ApplicationUser
        public ApplicationUser User { get; set; } // Navigation property to ApplicationUser

        public bool IsShown { get; set; } = true;

        public bool IsActive { get; set; } = false;

        public bool IsRepresentative { get; set; }

        public virtual ICollection<Warehouse>? Warehouses { get; set; }

        public bool IsAllOrdersHidden { get; set; }
    }
}
