using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lotus_blue.Models
{
    [Table("UserSwitchGroupMembers")]
    public class UserSwitchGroupMember
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserSwitchGroupId { get; set; }

        [Required]
        [MaxLength(450)]
        public string ApplicationUserId { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? DisplayName { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(UserSwitchGroupId))]
        public UserSwitchGroup? UserSwitchGroup { get; set; }

        [ForeignKey(nameof(ApplicationUserId))]
        public ApplicationUser? User { get; set; }
    }
}
