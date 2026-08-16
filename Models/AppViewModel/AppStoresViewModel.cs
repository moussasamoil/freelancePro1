using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models.AppViewModel
{
    public class AppStoresViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Logo { get; set; } // Logo URL property
        public string InvoiceImage { get; set; }
        public bool IsShown { get; set; }
    }




    public class AppCreateStoresViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(255, ErrorMessage = "Name must not exceed 255 characters")]
        public string Name { get; set; }
    }




}
