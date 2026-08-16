using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static lotus_blue.Models.Common;

namespace lotus_blue.Models
{
    public class Order
    {
        public bool IsPinned { get; set; } = false;
        public DateTime? PinnedAt { get; set; }
        public string? PinnedByUserId { get; set; }
        public int Id { get; set; }

        [Required]
        [Display(Name = "البلد")]
        public Countries Country { get; set; }

        [Display(Name = "الولاية")]
        [StringLength(255)]
        public string? State { get; set; }

        [Required]
        [Display(Name = "مصدر الطلب")]
        public OrderSourceEnum OrderSource { get; set; }

        [Display(Name = "اسم المصدر")]
        public string? SourceName { get; set; }

        [Display(Name = "شركة التصنيع")]
        public int? ManufacturingCompanyId { get; set; }

        public virtual ManufacturingCompany ManufacturingCompany { get; set; }

        [Display(Name = "شركة التوصيل")]
        public int DeliveryCompanyId { get; set; }

        public DeliveryCompany DeliveryCompany { get; set; }

        [Required]
        [Display(Name = "رقم الهاتف")]
        [StringLength(255)]
        public string TelephoneNumber { get; set; }

        [Display(Name = "رقم الهاتف الثاني")]
        [StringLength(255)]
        public string? SecondTelephoneNumber { get; set; }

        [Required]
        [Display(Name = "اسم العميل")]
        [StringLength(255)]
        public string CustomerName { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(255)]
        public string? Notes { get; set; }

        [Required]
        [Display(Name = "العنوان")]
        [StringLength(255)]
        public string Address { get; set; }

        public ICollection<OrderWarehouse> OrderWarehouses { get; set; }

        [Required]
        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedDate { get; set; }

        [Required]
        [Display(Name = "آخر تعديل")]
        public DateTime? LastEditedDate { get; set; }

        [Display(Name = "تاريخ آخر تعديل لمعالجة الطلب")]
        public DateTime? FixedOrderDate { get; set; }

        [Display(Name = "تاريخ إضافة فورية لمعالجة الطلب")]
        public DateTime? InstantAddedDate { get; set; }

        [Required]
        [Display(Name = "حالة الطلب")]
        public OrderStatusEnum OrderStatus { get; set; } = OrderStatusEnum.طلب_جديد;

        [Required]
        [Display(Name = "السعر الإجمالي")]
        public decimal TotalPrice { get; set; }

        [Required]
        [Display(Name = "السعر الإجمالي")]
        public decimal DeliveryPrice { get; set; }

        public int? ExternalOrderId { get; set; }

        [Display(Name = "المستخدم")]
        public string ApplicationUserId { get; set; }

        public ApplicationUser ApplicationUser { get; set; }

        public bool FromComments { get; set; } = false;

        public bool Gender { get; set; }

        public bool IsHidden { get; set; } = false;

        public bool IsDelayed { get; set; } = false;

        public string? Fixedby { get; set; }

        public string? DelegateEmployeeId { get; set; }

        public bool IsPaid { get; set; } = false;

        public string? Editedby { get; set; }

        public bool IsDiscount { get; set; } = false;

        public bool IsClientSpecial { get; set; } = false;

        public virtual ICollection<OrderReportOrder> OrderReportOrders { get; set; }

        public bool IsBonusPaidForEmployee { get; set; } = false;

        public bool IsComplaints { get; set; } = false;

        public bool IsBonus { get; set; } = false;

        public int? BonusPaymentId { get; set; }

        [ForeignKey(nameof(BonusPaymentId))]
        public virtual EmployeeBonusPayment? BonusPayment { get; set; }

        [Required]
        public string Chaturl { get; set; }

        [Display(Name = "الحملة")]
        public int? CampaignId { get; set; }

        [ForeignKey("CampaignId")]
        public virtual Campaign? Campaign { get; set; }

        [Display(Name = "مدة الإنشاء بالثواني")]
        public int? CreationDurationSeconds { get; set; }

        public string? PhotoUrl { get; set; }

        public string? PaymentReceiptUrl { get; set; }
    }

    public enum OrderStatusEnum
    {
        [Display(Name = "انتظار التجهيز")]
        طلب_جديد = 0,

        [Display(Name = "تم التجهيز")]
        تم_التجهيز = 2,

        [Display(Name = "معالج")]
        تم_المعالجة = 3,

        [Display(Name = "قيد التوصيل")]
        قيد_التوصيل = 4,

        [Display(Name = "قيد التوصيل")]
        تم_التسليم_المؤقت = 5,

        [Display(Name = "تم التسليم")]
        تم_التسليم = 6,

        [Display(Name = "فشل التسليم")]
        فشل_التسليم = 7,

        [Display(Name = "فشل التسليم 1")]
        فشل_التسليم_1 = 30,

        [Display(Name = "فشل التسليم 2")]
        فشل_التسليم_2 = 19,

        [Display(Name = "فشل التسليم 3")]
        فشل_التسليم_3 = 20,

        [Display(Name = "فشل التسليم 4")]
        فشل_التسليم_4 = 21,

        [Display(Name = "فشل التسليم 5")]
        فشل_التسليم_5 = 22,

        [Display(Name = "فشل التسليم 6")]
        فشل_التسليم_6 = 23,

        [Display(Name = "فشل التسليم 7")]
        فشل_التسليم_7 = 24,

        [Display(Name = "انتظار المعالجة")]
        انتظار_المعالجة = 8,

        [Display(Name = "الطلبات المرجعة")]
        الطلبات_المرجعة = 9,

        [Display(Name = "اخطاء مندوبين ومتابعة")]
        أخطاء_الشركات_والمندوبين = 10,

        [Display(Name = "تم الإلغاء")]
        تم_الإلغاء = 11,

        [Display(Name = "أرشيف المرجع")]
        أرشيف_المرجع = 15,

        [Display(Name = "الطلبات المؤجلة")]
        الطلبات_المؤجلة = 12,

        [Display(Name = "تم تحديث الرصيد")]
        تم_تحديث_الرصيد = 13,

        [Display(Name = "تم الدفع")]
        تم_الدفع = 14,

        [Display(Name = "الطلبات المعلقة")]
        الطلبات_المعلقة = 16,

        [Display(Name = "الطلبات الغير معرفة")]
        الطلبات_الغير_معرفة = 17,

        [Display(Name = "الطلبات الغير مكتملة")]
        الطلبات_الغير_مكتملة = 18,

        [Display(Name = "الطلبات الغير مكتملة 1")]
        الطلبات_الغير_مكتملة_1 = 31,

        [Display(Name = "الطلبات الغير مكتملة 2")]
        الطلبات_الغير_مكتملة_2 = 25,

        [Display(Name = "الطلبات الغير مكتملة 3")]
        الطلبات_الغير_مكتملة_3 = 26,

        [Display(Name = "الطلبات الغير مكتملة 4")]
        الطلبات_الغير_مكتملة_4 = 27,

        [Display(Name = "الطلبات الغير مكتملة 5")]
        الطلبات_الغير_مكتملة_5 = 28,

        [Display(Name = "الطلبات الغير مكتملة 6")]
        الطلبات_الغير_مكتملة_6 = 29,
    }

    public enum OrderStatusesForDeliveryCompanyAndRepresentativeEnum
    {
        [Display(Name = "انتظار التجهيز")]
        طلب_جديد = 0,

        [Display(Name = "تم التجهيز")]
        تم_التجهيز = 2,

        [Display(Name = "معالج")]
        تم_المعالجة = 3,

        [Display(Name = "قيد التوصيل")]
        قيد_التوصيل = 4,

        [Display(Name = "تم التسليم")]
        تم_التسليم = 6,

        [Display(Name = "تم تحديث الرصيد")]
        تم_تحديث_الرصيد = 13,

        [Display(Name = "انتظار المعالجة")]
        انتظار_المعالجة = 8,

        [Display(Name = "الطلبات الغير مكتملة")]
        الطلبات_الغير_مكتملة = 18,

        [Display(Name = "الطلبات الغير مكتملة 1")]
        الطلبات_الغير_مكتملة_1 = 31,

        [Display(Name = "الطلبات الغير مكتملة 2")]
        الطلبات_الغير_مكتملة_2 = 25,

        [Display(Name = "الطلبات الغير مكتملة 3")]
        الطلبات_الغير_مكتملة_3 = 26,

        [Display(Name = "الطلبات الغير مكتملة 4")]
        الطلبات_الغير_مكتملة_4 = 27,

        [Display(Name = "الطلبات الغير مكتملة 5")]
        الطلبات_الغير_مكتملة_5 = 28,

        [Display(Name = "الطلبات الغير مكتملة 6")]
        الطلبات_الغير_مكتملة_6 = 29,
    }

    public enum OrderStatusesForEmployeesEnum
    {
        [Display(Name = "معالج")]
        تم_المعالجة = 3,

        [Display(Name = "قيد التوصيل")]
        تم_التسليم_المؤقت = 5,

        [Display(Name = "تم التسليم")]
        تم_التسليم = 6,

        [Display(Name = "فشل التسليم")]
        فشل_التسليم = 7,

        [Display(Name = "فشل التسليم 1")]
        فشل_التسليم_1 = 30,

        [Display(Name = "فشل التسليم 2")]
        فشل_التسليم_2 = 19,

        [Display(Name = "فشل التسليم 3")]
        فشل_التسليم_3 = 20,

        [Display(Name = "فشل التسليم 4")]
        فشل_التسليم_4 = 21,

        [Display(Name = "فشل التسليم 5")]
        فشل_التسليم_5 = 22,

        [Display(Name = "فشل التسليم 6")]
        فشل_التسليم_6 = 23,

        [Display(Name = "فشل التسليم 7")]
        فشل_التسليم_7 = 24,

        [Display(Name = "انتظار المعالجة")]
        انتظار_المعالجة = 8,

        [Display(Name = "الطلبات المرجعة")]
        الطلبات_المرجعة = 9,

        [Display(Name = "اخطاء مندوبين ومتابعة")]
        أخطاء_التوصيل = 10,

        [Display(Name = "تم الإلغاء")]
        تم_الإلغاء = 11,

        [Display(Name = "الطلبات الغير مكتملة 2")]
        الطلبات_الغير_مكتملة_2 = 25,

        [Display(Name = "الطلبات الغير مكتملة 3")]
        الطلبات_الغير_مكتملة_3 = 26,

        [Display(Name = "الطلبات الغير مكتملة 4")]
        الطلبات_الغير_مكتملة_4 = 27,

        [Display(Name = "الطلبات الغير مكتملة 5")]
        الطلبات_الغير_مكتملة_5 = 28,

        [Display(Name = "الطلبات الغير مكتملة 6")]
        الطلبات_الغير_مكتملة_6 = 29,
    }

    public enum OrderStatusesForFollowUpDepartmentEnum
    {
        [Display(Name = "انتظار التجهيز")]
        طلب_جديد = 0,

        [Display(Name = "تم التجهيز")]
        تم_التجهيز = 2,

        [Display(Name = "معالج")]
        تم_المعالجة = 3,

        [Display(Name = "قيد التوصيل")]
        قيد_التوصيل = 4,

        [Display(Name = "قيد التوصيل")]
        تم_التسليم_المؤقت = 5,

        [Display(Name = "تم التسليم")]
        تم_التسليم = 6,

        [Display(Name = "فشل التسليم")]
        فشل_التسليم = 7,

        [Display(Name = "فشل التسليم 1")]
        فشل_التسليم_1 = 30,

        [Display(Name = "فشل التسليم 2")]
        فشل_التسليم_2 = 19,

        [Display(Name = "فشل التسليم 3")]
        فشل_التسليم_3 = 20,

        [Display(Name = "فشل التسليم 4")]
        فشل_التسليم_4 = 21,

        [Display(Name = "فشل التسليم 5")]
        فشل_التسليم_5 = 22,

        [Display(Name = "فشل التسليم 6")]
        فشل_التسليم_6 = 23,

        [Display(Name = "فشل التسليم 7")]
        فشل_التسليم_7 = 24,

        [Display(Name = "انتظار المعالجة")]
        انتظار_المعالجة = 8,

        [Display(Name = "الطلبات المرجعة")]
        الطلبات_المرجعة = 9,

        [Display(Name = "اخطاء مندوبين ومتابعة")]
        أخطاء_التوصيل = 10,

        [Display(Name = "تم الإلغاء")]
        تم_الإلغاء = 11,

        [Display(Name = "تم تحديث الرصيد")]
        تم_تحديث_الرصيد = 13,

        [Display(Name = "الطلبات الغير معرفة")]
        الطلبات_الغير_معرفة = 17,

        [Display(Name = "الطلبات المعلقة")]
        الطلبات_المعلقة = 16,

        [Display(Name = "الطلبات الغير مكتملة")]
        الطلبات_الغير_مكتملة = 18,

        [Display(Name = "الطلبات الغير مكتملة 1")]
        الطلبات_الغير_مكتملة_1 = 31,

        [Display(Name = "الطلبات الغير مكتملة 2")]
        الطلبات_الغير_مكتملة_2 = 25,

        [Display(Name = "الطلبات الغير مكتملة 3")]
        الطلبات_الغير_مكتملة_3 = 26,

        [Display(Name = "الطلبات الغير مكتملة 4")]
        الطلبات_الغير_مكتملة_4 = 27,

        [Display(Name = "الطلبات الغير مكتملة 5")]
        الطلبات_الغير_مكتملة_5 = 28,

        [Display(Name = "الطلبات الغير مكتملة 6")]
        الطلبات_الغير_مكتملة_6 = 29,
    }

    public enum FailureReasonEnum
    {
        [Display(Name = "لم يرد على الهاتف")]
        لم_يرد_على_الهاتف = 1,

        [Display(Name = "رقم الهاتف مغلق")]
        رقم_الهاتف_مغلق = 2,

        [Display(Name = "رقم الهاتف غير فعال")]
        رقم_الهاتف_غير_فعال = 3,

        [Display(Name = "رفض استلام الطلب")]
        رفض_استلام_الطلب = 4,

        [Display(Name = "العميل غير متاح للاستلام")]
        العميل_غير_متاح_للاستلام = 5,

        [Display(Name = "العميل مسافر")]
        العميل_مسافر = 6,

        [Display(Name = "رفض بسبب عدم الفحص")]
        رفض_بسبب_عدم_الفحص = 7,

        [Display(Name = "المبلغ المطلوب غير متوفر")]
        المبلغ_المطلوب_غير_متوفر = 8,

        [Display(Name = "تأجيل الاستلام")]
        تأجيل_التوصيل = 9,

        [Display(Name = "خطأ بالطلبية")]
        طلب_خاطئ = 10,

        [Display(Name = "الطلب غير مطابق للمطلوب")]
        الطلب_لا_يطابق_المطلوب = 11,
    }

    public enum OrderSourceEnum
    {
        فيسبوك = 1,
        انستغرام = 2,
        سناب_شات = 3,
        واتساب = 4,
        تك_توك = 5,
        توتير = 6,
        ويبسات = 7,
        اتصال = 8,
        تلغرام = 9,
        ميتا = 10
    }
}