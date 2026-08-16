using lotus_blue.Models.ViewModel;
using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models.AppViewModel
{
    public class AppWarehouseViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Price { get; set; }
        public int UnchangingAmount { get; set; }
        public int Amount { get; set; }
        public string Total { get; set; }
        public string ProductImage { get; set; }
        public AppCompanyViewModel DeliveryCompany { get; set; }
        public AppCompanyViewModel ManufacturingCompany { get; set; }
        public string DateAdded { get; set; }
        public string DateUpdated { get; set; }
        public int CountryId { get; set; } // Assuming you're using an int to represent the country
        public string? City { get; set; }
        public bool IsShown { get; set; }

    }


    public class AppCreateWarehouseViewModel
    {
        [Required(ErrorMessage = "The Name field is required.")]
        [StringLength(150, ErrorMessage = "Name must be at most 150 characters.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "The Price field is required.")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "The Amount field is required.")]
        public int Amount { get; set; }

        [Required(ErrorMessage = "The DeliveryCompanyid field is required.")]
        public int? DeliveryCompanyId { get; set; }

        [Required(ErrorMessage = "The ManufacturingCompanyid field is required.")]
        public int? ManufacturingCompanyId { get; set; }

        public string? City { get; set; }

        [Required(ErrorMessage = "The Country field is required.")]
        public int? CountryId { get; set; }
    }


}
