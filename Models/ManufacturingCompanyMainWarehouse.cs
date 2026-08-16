using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lotus_blue.Models
{
    public class ManufacturingCompanyMainWarehouse
    {
        public int Id { get; set; }

        [Required]
        public int ManufacturingCompanyId { get; set; }

        [Required]
        public int MainWarehouseId { get; set; }

        [ForeignKey(nameof(ManufacturingCompanyId))]
        public ManufacturingCompany? ManufacturingCompany { get; set; }

        [ForeignKey(nameof(MainWarehouseId))]
        public MainWarehouse? MainWarehouse { get; set; }
    }
}