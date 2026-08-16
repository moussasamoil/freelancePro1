using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lotus_blue.Models
{
    [Table("OrderInvestigationOpenings")]
    public class OrderInvestigationOpening
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        [StringLength(450)]
        public string ApplicationUserId { get; set; } = string.Empty;

        [StringLength(250)]
        public string? EmployeeName { get; set; }

        [Required]
        public DateTime OpenedAt { get; set; }

        [ForeignKey(nameof(OrderId))]
        public Order? Order { get; set; }
    }
}
