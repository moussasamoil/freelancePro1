using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models.ViewModel
{
    public class PriceOfferViewModel
    {
        public string? Country { get; set; }

        public string? City { get; set; }

        public string? DeliveryCompanyName { get; set; }

        public string? DeliveryCompanyAddress { get; set; }

        public string? DeliveryCompanyPhoneNumber { get; set; }

        public string? DeliveryCompanyEmail { get; set; }

        public DateTime CreatedDate { get; set; }
        public int InvoiceId { get; set; }
        public List<ProductViewModel> Products { get; set; } = new List<ProductViewModel>();
        public decimal TotalPriceOfAllProducts { get; set; }
    }

    public class ProductViewModel
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Amount { get; set; }
        public decimal TotalPrice => Price * Amount;

        public int UnchangingAmount { get; set; }

        public int TotalSoldAmount { get; set; }
    }

}
