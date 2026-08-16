using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System;
using System.Collections.Generic;
namespace lotus_blue.Models
{
    public class EmployeeBonusPayment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string EmployeeId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public virtual ApplicationUser Employee { get; set; }

        [Required]
        public DateTime DatePaid { get; set; }

        public decimal AmountPaid { get; set; }

        public decimal ProExtraAmount { get; set; }

        public int TotalOrderCount { get; set; }

        public int ProOrderCount { get; set; }

        public int SuccessOrderCount { get; set; }

        public int ProcessingOrderCount { get; set; }

        public decimal ProcessingAmount { get; set; }

        public decimal SuccessAmount { get; set; }

        public virtual ICollection<Order> Orders { get; set; }
    }
}
