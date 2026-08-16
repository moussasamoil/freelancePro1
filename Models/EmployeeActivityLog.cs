using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using lotus_blue.Models;

namespace Crm_LotusBlue.Models
{
    [Table("EmployeeActivityLogs")]
    public class EmployeeActivityLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = "";

        public int? EmployeeId { get; set; }

        public string? EmployeeName { get; set; }

        public string? EmployeeEmail { get; set; }

        public string? EmployeeImageUrl { get; set; }

        [Column(TypeName = "date")]
        public DateTime ActivityDate { get; set; }

        public DateTime FirstSeenAt { get; set; }

        public DateTime LastSeenAt { get; set; }

        public DateTime? LastActivityAt { get; set; }

        public string? CurrentPage { get; set; }

        public bool IsTabActive { get; set; } = true;

        public int TotalOnlineSeconds { get; set; }

        public int TotalActiveSeconds { get; set; }

        public DateTime? LastHeartbeatAt { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }
    }
}