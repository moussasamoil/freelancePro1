using lotus_blue.Models;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
namespace lotus_blue.Models
{
    public class Employee
    {
        public int Id { get; set; }

        [DisplayName("السيرة الذاتية")]
        [StringLength(255)]
        public string? Cv { get; set; }

        [DisplayName("الصورة")]
        [StringLength(255)]
        public string? ImageUrl { get; set; }

        [DisplayName("الاسم")]
        [StringLength(100)]
        [Required(ErrorMessage = "هذا الحقل مطلوب")]
        public string? Name { get; set; }

        [DisplayName("اسم العرض")]
        [StringLength(100)]
        public string? DisplayName { get; set; }

        [DisplayName("رقم الهوية")]
        [StringLength(50)]
        [Required(ErrorMessage = "هذا الحقل مطلوب")]
        public string IdNumber { get; set; }

        [DisplayName("الجنسية")]
        [StringLength(100)]
        [Required(ErrorMessage = "هذا الحقل مطلوب")]
        public string Nationality { get; set; }

        [DisplayName("الدولة")]
        [StringLength(50)]
        public string? Country { get; set; }

        [DisplayName("الهاتف")]
        [StringLength(20)]
        [Required(ErrorMessage = "هذا الحقل مطلوب")]
        public string PhoneNumber { get; set; }

        [DisplayName("العنوان")]
        [StringLength(255)]
        [Required(ErrorMessage = "هذا الحقل مطلوب")]
        public string Address { get; set; }

        [DisplayName("الراتب")]
        [Required(ErrorMessage = "هذا الحقل مطلوب")]
        public decimal Salary { get; set; }

        [DisplayName("المستوى الأكاديمي")]
        [StringLength(100)]
        public string? AcademicLevel { get; set; }

        [DisplayName("المسمى الوظيفي")]
        [StringLength(100)]
        [Required(ErrorMessage = "هذا الحقل مطلوب")]
        public string JobTitle { get; set; }

        [DisplayName("تاريخ الميلاد")]
        public DateTime DateOfBirth { get; set; }

        [DisplayName("الجنس")]
        [Required(ErrorMessage = "هذا الحقل مطلوب")]
        public bool Gender { get; set; }

        [DisplayName("تاريخ الإضافة")]
        [Required(ErrorMessage = "هذا الحقل مطلوب")]
        public DateTime DateAdded { get; set; } = DateTime.Now;

        [DisplayName("الشركة")]
        public int? DeliveryCompanyId { get; set; }

        public DeliveryCompany DeliveryCompany { get; set; }

        [Required(ErrorMessage = "هذا الحقل مطلوب")]
        public string ApplicationUserId { get; set; }

        public ApplicationUser ApplicationUser { get; set; }

        public bool IsShown { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public string? IdCardFrontImage { get; set; }
        [StringLength(100)]
        public string? WeeklyOffDays { get; set; }
        public bool AllowMobileOrTabletLogin { get; set; } = false;
        public bool ApplyShiftAccess { get; set; } = true;
        public string? IdCardBackImage { get; set; }
    }
}
