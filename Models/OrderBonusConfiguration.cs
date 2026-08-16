using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models
{
    public class OrderBonusConfiguration
    {
        public int Id { get; set; }
        [DisplayName("السعر الأجمالي للطلب")]
        public decimal OrderThreshold { get; set; } // The order amount threshold for flat bonus

        [DisplayName("العمولة لمبلغ الطلب")]
        public decimal FlatBonusAmount { get; set; } // The flat bonus amount for orders above the threshold

        [DisplayName("عمولة لجميع الطلبات")]
        public decimal? PercentageBonus { get; set; } // The percentage of the order's total price to be given as bonus

        [Required(ErrorMessage = "هذا الحقل مطلوب ")]
        public Common.Countries Country { get; set; }

        public int? EmployeeId { get; set; } // Make EmployeeId nullable by changing the type to int?
        public Employee Employee { get; set; }
    }
}
