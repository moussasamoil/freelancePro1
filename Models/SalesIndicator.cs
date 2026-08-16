using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static lotus_blue.Models.Common;

namespace lotus_blue.Models
{
    public class SalesIndicator
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "الدولة")]
        public Countries Country { get; set; }

        [Required]
        [Display(Name = "المنتج الرئيسي")]
        public int MainWarehouseId { get; set; }

        [ForeignKey(nameof(MainWarehouseId))]
        public virtual MainWarehouse? MainWarehouse { get; set; }

        [Required]
        [Display(Name = "الحد الأدنى للبيع من")]
        public decimal MinimumSellingFrom { get; set; }

        [Required]
        [Display(Name = "الحد الأدنى للبيع إلى")]
        public decimal MinimumSellingTo { get; set; }

        [Required]
        [Display(Name = "الحد الأساسي للبيع من")]
        public decimal BasicSellingFrom { get; set; }

        [Required]
        [Display(Name = "الحد الأساسي للبيع إلى")]
        public decimal BasicSellingTo { get; set; }

        [Required]
        [Display(Name = "الحد الوسطي للبيع من")]
        public decimal MiddleSellingFrom { get; set; }

        [Required]
        [Display(Name = "الحد الوسطي للبيع إلى")]
        public decimal MiddleSellingTo { get; set; }

        public string? CreatedByUserId { get; set; }
        public string? UpdatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}