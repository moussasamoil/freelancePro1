using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models.ViewModel
{
    public class ManufacturingCompanyViewModel
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [StringLength(200)]
        public string? Logo { get; set; }

        public bool IsShown { get; set; } = true;

        public string? InvoiceImage { get; set; }

        public string? ImageUrl2 { get; set; }

        public string? PhoneNumber { get; set; }
        public List<int> MainWarehouseIds { get; set; } = new();
        public int? MainWarehouseId { get; set; }
    }
}
