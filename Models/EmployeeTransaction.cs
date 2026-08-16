using System;
using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models
{
    public class EmployeeTransaction
    {
        public int Id { get; set; }

        [Display(Name = "المبلغ")]
        public decimal Amount { get; set; }

        [Display(Name = "نوع العملية")]
        public TransactionTypeEnum TransactionType { get; set; }

        [Display(Name = "السبب")]
        [StringLength(255)]
        public string? Reason { get; set; }

        [Display(Name = "تاريخ العملية")]
        public DateTime Date { get; set; } = DateTime.Now;

        [Display(Name = "معرف الموظف")]
        public int EmployeeId { get; set; }

        public Employee Employee { get; set; }

        // Foreign key to EmployeePaymentSummary
        public int? EmployeePaymentSummaryId { get; set; }
        public EmployeePaymentSummary? EmployeePaymentSummary { get; set; }

        // Soft delete / trash
        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        [StringLength(250)]
        public string? DeletedByUserName { get; set; }

        // JSON list for edit history entries
        public string? EditHistoryJson { get; set; }
    }

    public enum TransactionTypeEnum
    {
        خصم = 0,
        مكافأة = 1,
        سلفة = 2
    }
}
