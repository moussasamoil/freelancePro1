using lotus_blue.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using static lotus_blue.Models.Common;

namespace lotus_blue.Models
{
    public class Campaign
    {
        public int Id { get; set; }

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        public string? Name { get; set; }

        [Display(Name = "البلد")]
        public Countries Country { get; set; }

        [Display(Name = "Main Product")]
        public int? MainWarehouseId { get; set; }

        [ForeignKey("MainWarehouseId")]
        public MainWarehouse? MainWarehouse { get; set; }

        [Display(Name = "المتجر")]
        public int? ManufacturingCompanyId { get; set; }

        [ForeignKey("ManufacturingCompanyId")]
        public ManufacturingCompany? ManufacturingCompany { get; set; }


        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;


        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;
    }
}
