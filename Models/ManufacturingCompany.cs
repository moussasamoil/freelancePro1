using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models
{
    public class ManufacturingCompany
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }

        [StringLength(200)]
        public string? ImageUrl { get; set; }

        public bool IsShown { get; set; } = false;

        public string? InvoiceImage { get; set; }
        public ICollection<ManufacturingCompanyMainWarehouse> ManufacturingCompanyMainWarehouses { get; set; } = new List<ManufacturingCompanyMainWarehouse>(); public ICollection<EmployeeManufacturingCompany> EmployeeManufacturingCompanies { get; set; }

        public string ImageUrl2 { get; set; }

        public string? PhoneNumber { get; set;}

        public int? MainWarehouseId { get; set; }
        public MainWarehouse? MainWarehouse { get; set; }
    }

}
