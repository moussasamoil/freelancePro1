using System;
using System.ComponentModel.DataAnnotations;
using static lotus_blue.Models.Common;

namespace lotus_blue.Models
{
    public class Lead
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "اسم الصفحة")]
        [StringLength(255)]
        public string SourceName { get; set; }

        [Required]
        [Display(Name = "نوع الصفحة")]
        public OrderSourceEnum OrderSource { get; set; }

        [Display(Name = "رقم الهاتف")]
        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        [Display(Name = "رابط الصفحة")]
        public string? ChatUrl { get; set; }

        [Required]
        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedDate { get; set; }

        [Required]
        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
    }
}
