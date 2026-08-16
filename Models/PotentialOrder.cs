using System;
using System.ComponentModel.DataAnnotations;
using static lotus_blue.Models.Common;

namespace lotus_blue.Models
{
    public class PotentialOrder
    {
        public int Id { get; set; }

        [Display(Name = "اسم العميل")]
        [StringLength(255)]
        public string? CustomerName { get; set; }

        [Required]
        [Display(Name = "البلد")]
        public Countries Country { get; set; }

        [Display(Name = "رابط المحادثة")]
        public string? ChatUrl { get; set; }

        [Required]
        [Display(Name = "اسم المتجر")]
        [StringLength(255)]
        public string StoreName { get; set; }

        [Display(Name = "رقم الهاتف")]
        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        [Required]
        [Display(Name = "نوع الصفحة")]
        public OrderSourceEnum OrderSource { get; set; }

        [Required]
        [Display(Name = "الحالة")]
        public PotentialOrderStatus Status { get; set; } = PotentialOrderStatus.عميل_محتمل;

        [Required]
        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedDate { get; set; }

        [Display(Name = "آخر تعديل")]
        public DateTime? LastEditedDate { get; set; }

        [Required]
        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
    }

    public enum PotentialOrderStatus
    {
        [Display(Name = "عميل محتمل")]
        عميل_محتمل = 0,

        [Display(Name = "الرجوع للزبون")]
        الرجوع_للزبون = 1,

        [Display(Name = "تم إرسال العرض 1")]
        تم_إرسال_العرض_1 = 2,

        [Display(Name = "تم إرسال العرض 2")]
        تم_إرسال_العرض_2 = 3,

        [Display(Name = "تم إرسال العرض 3")]
        تم_إرسال_العرض_3 = 4,

        [Display(Name = "تم إرسال العرض 4")]
        تم_إرسال_العرض_4 = 5,

        [Display(Name = "تم إرسال العرض 5")]
        تم_إرسال_العرض_5 = 6,

        [Display(Name = "تم إرسال العرض 6")]
        تم_إرسال_العرض_6 = 7
    }
}
