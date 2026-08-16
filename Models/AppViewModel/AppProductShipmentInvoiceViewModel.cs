using lotus_blue.Models.ViewModel;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations.Schema;

namespace lotus_blue.Models.AppViewModel
{
    public class AppProductShipmentInvoiceViewModel
    {
        public int Id { get; set; }

        public int CustomId { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public decimal TotalPrice { get; set; }
        public string CreatedDate { get; set; }
        public AppCompanyViewModel DeliveryCompany { get; set; }
        public List<WarehouseAmountViewModel> ProductShipmentInvoiceWarehouses { get; set; }
    }


   
}
