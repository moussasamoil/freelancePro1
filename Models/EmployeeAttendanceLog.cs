using lotus_blue.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm_LotusBlue.Models
{
    [Table("EmployeeAttendanceLogs")]
    public class EmployeeAttendanceLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        public int? EmployeeId { get; set; }

        public string? EmployeeEmail { get; set; }

        public string? EmployeeName { get; set; }

        public DateTime CheckInAt { get; set; } = DateTime.Now;

        public DateTime? CheckOutAt { get; set; }

        public string? FaceImagePath { get; set; }

        public string? CheckOutFaceImagePath { get; set; }

        public string? CheckInIpAddress { get; set; }

        public string? CheckInLocation { get; set; }

        public string? CheckOutIpAddress { get; set; }

        public string? CheckOutLocation { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SalaryAtCheckIn { get; set; }

        public decimal? DeductionAmount { get; set; }

        public string? DeductionReason { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }

        [NotMapped]
        public decimal TransactionDeductionTotalAmount { get; set; }

        [NotMapped]
        public decimal AdvanceAmount { get; set; }

        [NotMapped]
        public decimal BonusAmount { get; set; }
        public int? ShiftId { get; set; }
        public DateTime? ShiftStartAt { get; set; }
        public DateTime? ShiftEndAt { get; set; }
    }
}