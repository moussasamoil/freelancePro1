using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace lotus_blue.Models.ViewModel
{
    public class WarehouseViewModel
    {

        public int Id { get; set; }

        [DisplayName("الاسم")]
        [StringLength(255)]
        public string? Name { get; set; }

        [DisplayName("السعر")]
        [Required]
        public decimal Price { get; set; }

        [DisplayName("السعر الأجمالي")]
        public decimal Total { get; set; }


        [DisplayName("الكمية العالمية")]
        [Required]
        public int UnchangingAmount { get; set; }

        [DisplayName("الكمية")]
        [Required]
        public int Amount { get; set; }

        [DisplayName("صورة المنتج")]
        [StringLength(255)]
        public string? ProductImage { get; set; }

        [DisplayName("شركة التوصيل")]
        [Required]
        public int DeliveryCompanyId { get; set; }

        [DisplayName("شركة التصنيع")]
        [Required]
        public int ManufacturingCompanyId { get; set; }

        public DateTime DateUpdated { get; set; }

        // Other properties as needed

        public Common.Countries Countries { get; set; }

        public string? City { get; set; }

        public int MainWarehouseId { get; set; }

        public string? DeliveryCompanyName { get; set; }

        public string? ManufacturingCompanyName { get; set; }

        public DateTime? DateAdded { get; set; }

        public bool IsShown { get; set; }

        public string? ProductCode { get; set; }

        // Warehouse Edit History
        public List<WarehouseEditHistory> EditHistories { get; set; } = new List<WarehouseEditHistory>();

        // New property
        public int? TotalDeliveredItemsFromSpecificOrders { get; set; }

        public int? TotalFailedDeliveredItemsFromSpecificOrders { get; set; }

        [DisplayName("المستودع الفرعي")]
        [Required]
        public int SubWarehouseId { get; set; }
        public SubWarehouse? SubWarehouse { get; set; }

    }
}


