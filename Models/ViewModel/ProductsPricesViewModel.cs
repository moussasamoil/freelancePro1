using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace lotus_blue.Models.ViewModel
{
    public class ProductsPricesViewModel
    {
        public int Id { get; set; }

        public Common.Countries Country { get; set; }

        [Required(ErrorMessage = "The Product Name field is required.")]
        public string ProductName { get; set; }

        [Display(Name = "Product Image")]
        public string? ProductImage { get; set; }

        [Display(Name = "Product Price")]
        [Required(ErrorMessage = "The Product Price field is required.")]
        [Range(0, double.MaxValue, ErrorMessage = "Please enter a valid price.")]
        public decimal ProductPrice { get; set; }

        public string? ManufacturingCompanyName { get; set; } // Name of the manufacturing company


        [Display(Name = "Manufacturing Company")]
        public int SelectedManufacturingCompanyId { get; set; } // Selected manufacturing company ID

        public List<ManufacturingCompanyViewModel>? ManufacturingCompanies { get; set; } // Collection of available manufacturing companies
    }

  

}
