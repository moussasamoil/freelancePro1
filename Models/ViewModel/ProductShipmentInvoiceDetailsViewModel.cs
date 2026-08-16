using System.ComponentModel.DataAnnotations.Schema;
using Z.Expressions.Compiler;

namespace lotus_blue.Models.ViewModel
{
    public class ProductShipmentInvoiceDetailsViewModel
    {
        public int InvoiceId { get; set; }
        public string DeliveryCompanyName { get; set; }
        public DateTime CreatedDate { get; set; }       
        public string DeliveryCompanyPhoneNumber { get; set; }
        public string DeliveryCompanyAddress { get; set; }
        public string DeliveryCompanyEmail { get; set; }

        // Include other properties as needed, for example:
        public List<WarehouseDetail> WarehouseDetails { get; set; } = new List<WarehouseDetail>();
    }

    public class WarehouseDetail
    {
        public string WarehouseName { get; set; }
        public int Quantity { get; set; }
        // Add other details as necessary
        public decimal WarehousePrice { get; set; }

        public int UnchangingAmount { get; set; }

        public int TotalSoldAmount { get; set; }

    }
}


