using lotus_blue.Models.ViewModel;
using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models.AppViewModel
{
    public class AppProductsPricesViewModel
    {
        public int Id { get; set; }

        public Common.Countries Country { get; set; }

        public string ProductName { get; set; }

        public string? ProductImage { get; set; }

        public string ProductPrice { get; set; }

        public string ManufacturingCompanyName { get; set; } // Name of the manufacturing company

        public int SelectedManufacturingCompanyId { get; set; } // Selected manufacturing company ID

    }


    public class AppCreateProductsPricesViewModel
    {

        [Required]
        public Common.Countries Country { get; set; }

        [Required(ErrorMessage = "Product name is required")]
        public string ProductName { get; set; }

        // Assuming ProductImage is optional, so not marking it as required
        public string? ProductImage { get; set; }

        [Required(ErrorMessage = "Product price is required")]
        public decimal ProductPrice { get; set; }

        [Required(ErrorMessage = "Manufacturing company ID is required")]
        public int SelectedManufacturingCompanyId { get; set; } // Selected manufacturing company ID

    }

}
