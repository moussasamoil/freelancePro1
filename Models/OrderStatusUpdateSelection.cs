using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using lotus_blue.OrderStatus;

namespace lotus_blue.Models
{
    public class OrderStatusUpdateSelection
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [ForeignKey(nameof(OrderId))]
        public Order? Order { get; set; }

        [Required]
        public OrderStatusEnum TargetStatus { get; set; }

        [MaxLength(500)]
        public string? FailureReason { get; set; }

        [Required]
        [MaxLength(450)]
        public string SelectedByUserId { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? SelectedByName { get; set; }

        public DateTime SelectedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
