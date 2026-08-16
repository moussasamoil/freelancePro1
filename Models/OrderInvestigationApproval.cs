using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lotus_blue.Models
{
    [Table("OrderInvestigationApprovals")]
    public class OrderInvestigationApproval
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        [StringLength(450)]
        public string ApplicationUserId { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string EmployeeName { get; set; } = string.Empty;

        public DateTime ApprovedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(OrderId))]
        public Order? Order { get; set; }
    }
}
