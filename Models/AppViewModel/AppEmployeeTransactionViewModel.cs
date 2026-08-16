using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models.AppViewModel
{
    public class AppEmployeeTransactionViewModel
    {
        public int Id { get; set; }

        [Display(Name = "المبلغ")]
        public string Amount { get; set; }

        [Display(Name = "نوع العملية")]
        public string TransactionType { get; set; }

        public TransactionTypeEnum TransactionTypeEnum { get; set; }

        [Display(Name = "السبب")]
        [StringLength(255)]
        public string Reason { get; set; }

        [Display(Name = "تاريخ العملية")]
        public string DateCreated { get; set; }

        [Display(Name = "معرف الموظف")]
        public int EmployeeId { get; set; }

        [Display(Name = "اسم الموظف")]
        public string EmployeeName { get; set; }

    }



    public class AppCreateEmployeeTransactionViewModel
    {
        [Display(Name = "المبلغ")]
        [Required(ErrorMessage = "المبلغ مطلوب")]
        public decimal Amount { get; set; }

        [Display(Name = "نوع العملية")]
        [Required(ErrorMessage = "نوع العملية مطلوب")]
        public TransactionTypeEnum TransactionType { get; set; }

        [Display(Name = "السبب")]
        [StringLength(255)]
        public string Reason { get; set; }

        [Display(Name = "معرف الموظف")]
        [Required(ErrorMessage = "معرف الموظف مطلوب")]
        public int EmployeeId { get; set; }
    }


}
