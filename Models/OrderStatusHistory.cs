using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lotus_blue.Models
{
    public class OrderStatusHistory
    {
        public int Id { get; set; }

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "حالة الطلب")]
        public OrderStatusEnum? Status { get; set; }

        [Display(Name = "السبب")]
        [StringLength(255)]
        public string? Reason { get; set; }

        [Display(Name = "المستخدم")]
        public string? ApplicationUserId { get; set; }
        public ApplicationUser? User { get; set; }

        [Display(Name = "رقم الطلب")]
        public int? OrderId { get; set; }
        public Order Order { get; set; }
        public string? FailureReasonImageUrl { get; set; }

        public string? Name { get; set; }

        [Display(Name = "مخفي")]
        public bool IsHidden { get; set; } = false; // Default to false, meaning not hidden
    }
}
