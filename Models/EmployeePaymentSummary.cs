using System;
using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models
{
    public class EmployeePaymentSummary
    {
        public int Id { get; set; }

        [Display(Name = "الشهر")]
        public DateTime Month { get; set; } = DateTime.Now; // Set the default to the current month

        [Display(Name = "المبلغ المدفوع")]
        public decimal TotalPaid { get; set; }

        [Display(Name = "حساب جارٍ")]
        public decimal OngoingAccount { get; set; }

        [Display(Name = "إجمالي الخصومات")]
        public decimal TotalDeductions { get; set; }

        [Display(Name = "إجمالي المكافآت")]
        public decimal TotalBonuses { get; set; }

        [Display(Name = "إجمالي السلف")]
        public decimal TotalAdvances { get; set; }

        [Display(Name = "إجمالي الراتب")]
        public decimal TotalSalaryPaid { get; set; }

        [Display(Name = "اجمالي العمولات")]
        public decimal TotalCommissions { get; set; } // New Property

        [Display(Name = "معرف الموظف")]
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; }


    }
}
