using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models.AppViewModel
{
    public class AppDeliveryCompanyViewModel
    {
        public int Id { get; set; }
        public string LogoUrl { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string Specialty { get; set; }
        public Common.Countries Country { get; set; }
        public bool IsShown { get; set; }
        public bool IsActive { get; set; }

        public string Notes { get; set; }
        public string TaxRegistrationNumber { get; set; }

        public string IdNumber { get; set; }

        public string Address { get; set; }
    }


    public class AppCreateDeliveryCompanyViewModel
    {

        [Display(Name = "اسم الشركة")]
        [Required(ErrorMessage = "اسم الشركة مطلوب.")]
        public string Name { get; set; }

     
        [Display(Name = "رقم التسجيل الضريبي")]
        [Required(ErrorMessage = "رقم التسجيل الضريبي مطلوب.")]
        public string TaxRegistrationNumber { get; set; }

        [Display(Name = "رقم الهوية")]
        [StringLength(20, ErrorMessage = "يجب أن يكون رقم الهوية أقل من 20 حرفًا.")]
        [Required]
        public string IdNumber { get; set; }


        [Display(Name = "العنوان")]
        [Required(ErrorMessage = "العنوان مطلوب.")]
        public string Address { get; set; }

        [Display(Name = "رقم الهاتف")]
        [RegularExpression(@"^\+?[0-9]{8,15}$", ErrorMessage = "رقم هاتف غير صالح.")]
        [Required(ErrorMessage = "رقم الهاتف مطلوب.")]
        public string PhoneNumber { get; set; }

        [Display(Name = "التخصص")]
        [Required(ErrorMessage = "التخصص مطلوب.")]
        public string Specialty { get; set; }

     
        [Display(Name = "الموقع الإلكتروني")]
        public string Website { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(500, ErrorMessage = "يجب أن تكون الملاحظات أقل من 500 حرف.")]
        public string Notes { get; set; }

        [Display(Name = "البلد المحدد")]
        [Required(ErrorMessage = "يرجى اختيار البلد.")]
        public Common.Countries SelectedCountry { get; set; }

        [Display(Name = "المدينة")]
        public string? City { get; set; }

        // register view model 
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


    }


    public class AppEditDeliveryCompanyViewModel
    {
        public int Id { get; set; }

        [StringLength(255, ErrorMessage = "Email must not exceed 255 characters")]
        public string Email { get; set; }

        [StringLength(255, ErrorMessage = "Password must not exceed 255 characters")]
        public string? NewPassword { get; set; }

        [StringLength(255, ErrorMessage = "Password must not exceed 255 characters")]
        public string? ConfirmNewPassword { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(255, ErrorMessage = "Name must not exceed 255 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Tax registration number is required")]
        [StringLength(255, ErrorMessage = "Tax registration number must not exceed 255 characters")]
        public string TaxRegistrationNumber { get; set; }

        [Required(ErrorMessage = "ID number is required")]
        [StringLength(255, ErrorMessage = "ID number must not exceed 255 characters")]
        public string IdNumber { get; set; }

        [Required(ErrorMessage = "Address is required")]
        [StringLength(255, ErrorMessage = "Address must not exceed 255 characters")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(255, ErrorMessage = "Phone number must not exceed 255 characters")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Specialty is required")]
        [StringLength(255, ErrorMessage = "Specialty must not exceed 255 characters")]
        public string Specialty { get; set; }

        [StringLength(255, ErrorMessage = "Website must not exceed 255 characters")]
        public string? Website { get; set; }

        [StringLength(255, ErrorMessage = "Notes must not exceed 255 characters")]
        public string? Notes { get; set; }

        [Required(ErrorMessage = "Selected country is required")]
        public Common.Countries SelectedCountry { get; set; }
    }


    public class AppDeliveryCompanyPriceViewModel
    {
        public int Id { get; set; }
        public string Currency { get; set; }   
        public Common.Countries SelectedCountry { get; set; }

        [Display(Name = "السعر")]
        [Required(ErrorMessage = "يجب تحديد السعر")]
        public decimal Price { get; set; }

        [Display(Name = "المدينة")]
        public string? City { get; set; }

        public int DeliveryCompanyId { get; set; }

        public string DeliveryCompanyName { get; set; }
    }

    public class AppCreateDeliveryCompanyPriceViewModel
    {
        [Required(ErrorMessage = "يجب تحديد البلد")]

        public Common.Countries? Country { get; set; }
        [Display(Name = "السعر")]
        [Required(ErrorMessage = "يجب تحديد السعر")]
        public decimal Price { get; set; }

        [Display(Name = "المدينة")]
        [Required(ErrorMessage = "يجب تحديد المدينة")]
        public string? City { get; set; }

        public int DeliveryCompanyId { get; set; }
    }


}
