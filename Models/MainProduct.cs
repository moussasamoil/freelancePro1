using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lotus_blue.Models
{
    public class MainProduct
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Please select a country.")]
        public Common.Countries Country { get; set; }

        [Required(ErrorMessage = "The product name is required.")]
        public string Name { get; set; }

        public string? ImageUrl { get; set; }

        [Required(ErrorMessage = "The product price is required.")]
        public decimal Price { get; set; }

        // Foreign key property
        public int ManufacturingCompanyId { get; set; }
        public ManufacturingCompany ManufacturingCompany { get; set; }
    }
}
