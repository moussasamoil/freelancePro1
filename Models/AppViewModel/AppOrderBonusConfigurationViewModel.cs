using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models.AppViewModel
{
    public class AppOrderBonusConfigurationViewModel
    {
        public int Id { get; set; }

        [DisplayName("السعر الأجمالي للطلب")]
        public string OrderThreshold { get; set; } // The order amount threshold for flat bonus

        [DisplayName("العمولة لمبلغ الطلب")]
        public string FlatBonusAmount { get; set; } // The flat bonus amount for orders above the threshold

        [DisplayName("عمولة لجميع الطلبات")]
        public string PercentageBonus { get; set; } // The percentage of the order's total price to be given as bonus

        public Common.Countries? countries { get; set; }

        public string Employeename { get; set; }
    }


    public class AppCreateOrderBonusConfigurationViewModel
    {
        [Required(ErrorMessage = "السعر الأجمالي للطلب مطلوب")]
        [DisplayName("السعر الأجمالي للطلب")]
        public decimal OrderThreshold { get; set; }

        [Required(ErrorMessage = "عمولة لمبلغ الطلب مطلوبة")]
        [DisplayName("عمولة لمبلغ الطلب")]
        public decimal FlatBonusAmount { get; set; }

        [DisplayName("عمولة لجميع الطلبات")]
        public decimal? PercentageBonus { get; set; }

        // If EmployeeId is optional, you can make it nullable
        [DisplayName("معرف الموظف")]
        public int? EmployeeId { get; set; }

        [Required(ErrorMessage = "هذا الحقل مطلوب")]
        [DisplayName("عمولة لمبلغ الطلب")]
        public Common.Countries? Countries { get; set; }
    }



}
