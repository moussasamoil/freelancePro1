using lotus_blue.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm_LotusBlue.Models
{
    [Table("EmployeeWorkShifts")]
    public class EmployeeWorkShift
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public TimeSpan ShiftStartTime { get; set; }

        [Required]
        public TimeSpan ShiftEndTime { get; set; }

        [StringLength(100)]
        public string? AllowedIpAddress { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsLoginBlocked { get; set; } = false;

        public DateTime? LoginBlockedAt { get; set; }

        [StringLength(300)]
        public string? LoginBlockReason { get; set; }

        public DateTime? AdminUnblockedUntil { get; set; }

        public DateTime? AdminUnblockedAt { get; set; }

        [StringLength(450)]
        public string? AdminUnblockedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }
    }
}
