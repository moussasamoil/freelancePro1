using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lotus_blue.Models
{
    public class EmployeeErrorEditHistory
    {
        public int Id { get; set; }

        [Required]
        public int EmployeeErrorId { get; set; }

        public int EmployeeId { get; set; }

        public string? OldErrorText { get; set; }

        public string? NewErrorText { get; set; }

        [MaxLength(700)]
        public string? OldImageUrl { get; set; }

        [MaxLength(700)]
        public string? NewImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(450)]
        public string? EditedByUserId { get; set; }

        [MaxLength(250)]
        public string? EditedByUserName { get; set; }

        [ForeignKey(nameof(EmployeeErrorId))]
        public EmployeeError? EmployeeError { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }
    }
}
