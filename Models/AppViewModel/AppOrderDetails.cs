using lotus_blue.Models.ViewModel;
using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models.AppViewModel
{

    public class AppOrderListViewModel
    {
        public int Id { get; set; }
        public string TelephoneNumber { get; set; }
        public string CreatedDate { get; set; }
        public Common.Countries Country { get; set; }
        public string DeliveryCompanyName { get; set; }
        public OrderStatusEnum OrderStatus { get; set; }

    }

    public class AppOrderDetails
    {

        public string CreatedBy { get; set; }
        public string LastEditedBy { get; set; }
        public string lastEditedByImage { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal DeliveryCost { get; set; }
        public decimal RemainingPrice { get; set; }
        public string? Currency { get; set; }

        public int Id { get; set; }
        public OrderStatusEnum OrderStatus { get; set; }

        public string SourceName { get; set; }
        public Common.Countries Country { get; set; }
        public string State { get; set; }
        public string Address { get; set; }

        public string TelephoneNumber { get; set; }
        public string SecondTelephoneNumber { get; set; }
        public string CustomerName { get; set; }
        public OrderSourceEnum OrderSource { get; set; }

        public string Notes { get; set; }
        public bool Gender { get; set; }
        public bool FromComments { get; set; }
        public int NumberOfEntries { get; set; }
        public string LastEditedDate { get; set; }
        public List<AppWarehouseAmountViewModel> SelectedWarehouses { get; set; }

        public AppCompanyViewModel ManufacturingCompany { get; set; }

        public AppCompanyViewModel DeliveryCompany { get; set; }

        public string CancelReasonForCancellation { get; set; }
        public string CancelReasonForDeliveryFailure { get; set; }

        public string EmployeeName { get; set; }

        public string EmployeeImage { get; set; }
    }


    public class AppOrderCreateViewModel
    {
        [Required(ErrorMessage = "حقل البلد مطلوب.")]
        [Display(Name = "البلد")]
        public Common.Countries Country { get; set; }

        [Required(ErrorMessage = "حقل الولاية مطلوب.")]
        [Display(Name = "الولاية")]
        [StringLength(255)]
        public string State { get; set; }

        [Required(ErrorMessage = "حقل مصدر الطلب مطلوب.")]
        [Display(Name = "مصدر الطلب")]
        public OrderSourceEnum OrderSource { get; set; }

        [Required(ErrorMessage = "حقل اسم المصدر مطلوب.")]
        [Display(Name = "اسم المصدر")]
        [StringLength(255)]
        public string SourceName { get; set; }

        [Required(ErrorMessage = "حقل شركة التصنيع مطلوب.")]
        [Display(Name = "شركة التصنيع")]
        public int ManufacturingCompanyId { get; set; }

        public int? StoreId { get; set; }

        [Required(ErrorMessage = "حقل شركة التوصيل مطلوب.")]
        [Display(Name = "شركة التوصيل")]
        public int DeliveryCompanyId { get; set; }

        [Required(ErrorMessage = "حقل رقم الهاتف مطلوب.")]
        [Display(Name = "رقم الهاتف")]
        [StringLength(255)]
        public string TelephoneNumber { get; set; }

        [Display(Name = "رقم الهاتف الثاني")]
        [StringLength(255)]
        public string? SecondTelephoneNumber { get; set; }

        [Required(ErrorMessage = "حقل اسم العميل مطلوب.")]
        [Display(Name = "اسم العميل")]
        [StringLength(255)]
        public string CustomerName { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(255)]
        public string? Notes { get; set; }

        [Required(ErrorMessage = "حقل العنوان مطلوب.")]
        [Display(Name = "العنوان")]
        [StringLength(255)]
        public string Address { get; set; }

        [Required(ErrorMessage = "حقل تاريخ الإنشاء مطلوب.")]
        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedDate { get; set; }

        [Required(ErrorMessage = "حقل المبلغ الإجمالي مطلوب.")]
        [Display(Name = "المبلغ الإجمالي")]
        [DisplayFormat(DataFormatString = "{0:0.00}")]
        public decimal TotalPrice { get; set; }


        [Required(ErrorMessage = "حقل  الجنس مطلوب.")]
        [Display(Name = "الجنس")]
        public bool Gender { get; set; }

        // warehouse for order
        [Required(ErrorMessage = "حقل المستودع مطلوب.")]
        [Display(Name = "مستودع")]
        public List<AppWarehouseAmountViewModel> SelectedWarehouses { get; set; }
    }




    public class AppOrderEditViewModel
    {


        [Required(ErrorMessage = "حقل البلد مطلوب.")]
        [Display(Name = "البلد")]
        public Common.Countries Country { get; set; }

        [Required(ErrorMessage = "حقل الولاية مطلوب.")]
        [Display(Name = "الولاية")]
        [StringLength(255)]
        public string State { get; set; }

        [Required(ErrorMessage = "حقل مصدر الطلب مطلوب.")]
        [Display(Name = "مصدر الطلب")]
        public OrderSourceEnum OrderSource { get; set; }

        [Required(ErrorMessage = "حقل اسم المصدر مطلوب.")]
        [Display(Name = "اسم المصدر")]
        [StringLength(255)]
        public string SourceName { get; set; }

        [Required(ErrorMessage = "حقل شركة التصنيع مطلوب.")]
        [Display(Name = "شركة التصنيع")]
        public int ManufacturingCompanyId { get; set; }


        [Required(ErrorMessage = "حقل شركة التوصيل مطلوب.")]
        [Display(Name = "شركة التوصيل")]
        public int DeliveryCompanyId { get; set; }

        [Required(ErrorMessage = "حقل رقم الهاتف مطلوب.")]
        [Display(Name = "رقم الهاتف")]
        [StringLength(255)]
        public string TelephoneNumber { get; set; }

        [Display(Name = "رقم الهاتف الثاني")]
        [StringLength(255)]
        public string? SecondTelephoneNumber { get; set; }

        [Required(ErrorMessage = "حقل اسم العميل مطلوب.")]
        [Display(Name = "اسم العميل")]
        [StringLength(255)]
        public string CustomerName { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(255)]
        public string? Notes { get; set; }

        [Required(ErrorMessage = "حقل العنوان مطلوب.")]
        [Display(Name = "العنوان")]
        [StringLength(255)]
        public string Address { get; set; }

        [Required(ErrorMessage = "حقل تاريخ الإنشاء مطلوب.")]
        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedDate { get; set; }

        [Required(ErrorMessage = "حقل المبلغ الإجمالي مطلوب.")]
        [Display(Name = "المبلغ الإجمالي")]
        [DisplayFormat(DataFormatString = "{0:0.00}")]
        public decimal TotalPrice { get; set; }


        [Required(ErrorMessage = "حقل  الجنس مطلوب.")]
        [Display(Name = "الجنس")]
        public bool Gender { get; set; }

        // warehouse for order
        [Required(ErrorMessage = "حقل المستودع مطلوب.")]
        [Display(Name = "مستودع")]
        public List<AppWarehouseAmountViewModel> SelectedWarehouses { get; set; }
    }

    public class AppWarehouseAmountViewModel
    {
        [Required]
        public int WarehouseId { get; set; }

        [Required]
        public int Amount { get; set; }

        public string? WarehouseName { get; set; }

        public string? WarehouseLogo { get; set; }
    }

}
