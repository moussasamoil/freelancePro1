using Microsoft.AspNetCore.Http;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
namespace lotus_blue.Models.ViewModel
{
    public class EmployeeViewModel
    {
        public int Id { get; set; }

        [DisplayName("السيرة الذاتية")]
        [StringLength(255)]
        public string? Cv { get; set; }

        [DisplayName("الصورة")]
        [StringLength(255)]
        public string? Img { get; set; }

        [DisplayName("صورة البطاقة وش")]
        [StringLength(255)]
        public string? IdCardFrontImage { get; set; }

        [DisplayName("صورة البطاقة ظهر")]
        [StringLength(255)]
        public string? IdCardBackImage { get; set; }

        [DisplayName("صورة البطاقة وش")]
        public IFormFile? IdCardFrontImageFile { get; set; }

        [DisplayName("صورة البطاقة ظهر")]
        public IFormFile? IdCardBackImageFile { get; set; }

        [DisplayName("الاسم")]
        [StringLength(100)]
        [Required]
        public string Name { get; set; } = string.Empty;

        [DisplayName("رقم الهوية")]
        [StringLength(50)]
        public string? IdNumber { get; set; }

        [DisplayName("الجنسية")]
        [StringLength(100)]
        public string? Nationality { get; set; }

        [DisplayName("الهاتف")]
        [StringLength(20)]
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        [DisplayName("العنوان")]
        [StringLength(255)]
        [Required]
        public string Address { get; set; } = string.Empty;

        [DisplayName("الراتب")]
        [Required]
        public decimal Salary { get; set; }

        [DisplayName("المستوى الأكاديمي")]
        [StringLength(100)]
        public string? AcademicLevel { get; set; }

        [DisplayName("المسمى الوظيفي")]
        [StringLength(100)]
        public string? JobTitle { get; set; }

        [DisplayName("تاريخ الميلاد")]
        [Required]
        public DateTime DateOfBirth { get; set; }

        [DisplayName("الجنس")]
        [Required]
        public bool Gender { get; set; }

        [DisplayName("تاريخ الإضافة")]
        [Required]
        public DateTime DateAdded { get; set; } = DateTime.Now;

        [DisplayName("الشركة")]
        public int? DeliveryCompanyId { get; set; }

        [Display(Name = "البريد الإلكتروني")]
        [EmailAddress(ErrorMessage = "عنوان البريد الإلكتروني غير صالح.")]
        public string? Email { get; set; }

        [Display(Name = "كلمة المرور")]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [Display(Name = "تأكيد كلمة المرور")]
        [Compare("Password", ErrorMessage = "كلمة المرور وتأكيد كلمة المرور غير متطابقين.")]
        [DataType(DataType.Password)]
        public string? ConfirmPassword { get; set; }

        public string? Role { get; set; }

        public List<string>? Roles { get; set; }

        public List<DeliveryCompany>? DeliveryCompanies { get; set; }

        public bool IsShown { get; set; } = true;

        public bool IsActive { get; set; }

        public string? NewPassword { get; set; }

        public string? ConfirmNewPassword { get; set; }

        [Display(Name = "اسم عرض الموظف")]
        public string? DisplayName { get; set; }

        [DisplayName("صلاحيات المتاجر")]
        public List<int> SelectedManufacturingCompanyIds { get; set; } = new List<int>();
        public List<string> WeeklyOffDays { get; set; } = new List<string>();

        public bool ApplyShiftAccess { get; set; } = true;
        public List<ManufacturingCompany> ManufacturingCompanies { get; set; } = new List<ManufacturingCompany>();
    }
}