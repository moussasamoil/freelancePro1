using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lotus_blue.Models
{
    public class EmployeeError
    {
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public string ErrorText { get; set; } = string.Empty;

        [MaxLength(700)]
        public string? ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(450)]
        public string? CreatedByUserId { get; set; }

        [MaxLength(250)]
        public string? CreatedByUserName { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [MaxLength(450)]
        public string? UpdatedByUserId { get; set; }

        [MaxLength(250)]
        public string? UpdatedByUserName { get; set; }

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        [MaxLength(450)]
        public string? DeletedByUserId { get; set; }

        [MaxLength(250)]
        public string? DeletedByUserName { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }
    }
}
