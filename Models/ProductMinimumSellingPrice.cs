using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lotus_blue.Models
{
    [Table("ProductMinimumSellingPrices")]
    public class ProductMinimumSellingPrice
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Common.Countries Country { get; set; }

        [Required]
        public int ManufacturingCompanyId { get; set; }

        [ForeignKey(nameof(ManufacturingCompanyId))]
        public ManufacturingCompany ManufacturingCompany { get; set; }

        [Required]
        public int MainWarehouseId { get; set; }

        [ForeignKey(nameof(MainWarehouseId))]
        public MainWarehouse MainWarehouse { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MinimumSellingPrice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }
    }
}
