using Microsoft.AspNetCore.Mvc.Rendering; // Required for SelectListItem
using System;
using System.Collections.Generic;

namespace lotus_blue.Models.ViewModel
{
    public class ProductShipmentInvoiceViewModel
    {
        public int Id { get; set; }
        public int? CustomId { get; set; }

        public Common.Countries Country { get; set; }
        public string? City { get; set; }
        public int DeliveryCompanyId { get; set; }       
        public string? DeliveryCompanyName { get; set; }
        public DateTime CreatedDate { get; set; }
        public decimal TotalPrice { get; set; }
        // Properties for handling multiple warehouses and quantities
        public List<WarehouseAmountViewModel> WarehouseQuantities { get; set; } = new List<WarehouseAmountViewModel>();

    }
}

