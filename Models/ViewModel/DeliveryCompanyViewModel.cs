using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models.ViewModel
{
    public class DeliveryCompanyViewModel
    {
        public int Id { get; set; }

        [Display(Name = "اسم الشركة")]
        [Required(ErrorMessage = "اسم الشركة مطلوب.")]
        public string Name { get; set; }

        [Display(Name = "لوغو الشركة ")]
        public string? Logo { get; set; }

        [Display(Name = " معلومات الشركة صورة  ")]
        public string? InformationUrl { get; set; }

        [Display(Name = "رقم التسجيل الضريبي")]
        public string? TaxRegistrationNumber { get; set; }

        [Display(Name = "رقم الهوية")]
        [StringLength(20, ErrorMessage = "يجب أن يكون رقم الهوية أقل من 20 حرفًا.")]
        public string? IdNumber { get; set; }

        [Display(Name = "العنوان")]
        [Required(ErrorMessage = "العنوان مطلوب.")]
        public string Address { get; set; }

        [Display(Name = "رقم الهاتف")]
        [RegularExpression(@"^\+?[0-9]{8,15}$", ErrorMessage = "رقم هاتف غير صالح.")]
        [Required(ErrorMessage = "رقم الهاتف مطلوب.")]
        public string PhoneNumber { get; set; }

        [Display(Name = "التخصص")]
        public string? Specialty { get; set; }



        [Display(Name = "الموقع الإلكتروني")]
        public string? Website { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(500, ErrorMessage = "يجب أن تكون الملاحظات أقل من 500 حرف.")]
        public string? Notes { get; set; }

        [Display(Name = "البلد المحدد")]
        [Required(ErrorMessage = "يرجى اختيار البلد.")]
        public Common.Countries Country { get; set; }

        [Display(Name = "المدينة")]
        public string? City { get; set; }

        [Display(Name = "البريد الإلكتروني")]
        [Required(ErrorMessage = "عنوان البريد الإلكتروني مطلوب.")]
        [EmailAddress(ErrorMessage = "عنوان البريد الإلكتروني غير صالح.")]
        public string Email { get; set; }

        [Display(Name = "كلمة المرور")]
        [Required(ErrorMessage = "كلمة المرور مطلوبة.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "تأكيد كلمة المرور")]
        [Compare("Password", ErrorMessage = "كلمة المرور وتأكيد كلمة المرور غير متطابقين.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }

        [Display(Name = "اسم العرض")]
        public string DisplayName { get; set; }


        public bool IsActive { get; set; }
        public bool IsShown { get; set; }
        // for details page 
        public double? WaitingForPreparationCount { get; set; }
        public double? DeferredCount { get; set; }
        public double? PreparedCount { get; set; }
        public double? InDeliveryCount { get; set; }
        public double? DeliveredCount { get; set; }
        public double? DeliveryFailedCount { get; set; }
        public double? WaitingForProcessingCount { get; set; }
        public double? ReturnedOrdersCount { get; set; }
        public double? PaidCount { get; set; }


        // password edit 

        // Additional properties for Edit View
        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string? ConfirmNewPassword { get; set; }

        public bool IsAllOrdersHidden { get; set; }
    }
}
