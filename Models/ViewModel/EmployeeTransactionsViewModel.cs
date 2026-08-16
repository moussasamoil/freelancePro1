using System;
using System.ComponentModel.DataAnnotations;
using lotus_blue.Models;

namespace lotus_blue.Models.ViewModel
{
    public class EmployeeTransactionViewModel
    {
        public int Id { get; set; }

        [Display(Name = "المبلغ")]
        [Required(ErrorMessage = "المبلغ مطلوب")]
        public decimal Amount { get; set; }

        [Display(Name = "نوع العملية")]
        [Required(ErrorMessage = "نوع العملية مطلوب")]
        public TransactionTypeEnum TransactionType { get; set; }

        [Display(Name = "السبب")]
        [StringLength(255)]
        public string? Reason { get; set; }

        [Display(Name = "معرف الموظف")]
        [Required(ErrorMessage = "الموظف مطلوب")]
        public int EmployeeId { get; set; }

        [Display(Name = "اسم الموظف")]
        public string EmployeeName { get; set; } = "بدون اسم";

        public string EmployeeImagePath { get; set; } = "/static/circle-user-solid.svg";

        public bool EmployeeIsActive { get; set; }

        public string ShiftStartTimeText { get; set; } = "-";

        public string ShiftEndTimeText { get; set; } = "-";

        public decimal? DeductionAmount { get; set; }

        public decimal? AdvanceAmount { get; set; }

        public decimal? BonusAmount { get; set; }

        public string TransactionDate { get; set; } = string.Empty;

        public string TransactionDateOnly { get; set; } = string.Empty;

        public string TotalDiscountPriceTRY { get; set; } = "0";
    }
}
