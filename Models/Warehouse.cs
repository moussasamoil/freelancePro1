using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lotus_blue.Models
{
    public class Warehouse
    {
        public int Id { get; set; }

        [DisplayName("الاسم")]
        [StringLength(255)]
        public string? Name { get; set; }

        [DisplayName("السعر")]
        [Required(ErrorMessage = "حقل السعر مطلوب.")]
        public decimal Price { get; set; }

        [DisplayName("الكمية العالمية")]
        [Required(ErrorMessage = "حقل الكمية العالمية مطلوب.")]
        public int UnchangingAmount { get; set; }

        private int _amount;
        [DisplayName("الكمية")]
        [Required(ErrorMessage = "حقل الكمية مطلوب.")]
        public int Amount { get; set; }

        [NotMapped] // Not stored in the database, calculated property
        [DisplayName("الإجمالي")]
        public decimal Total => Price * Amount;


        [DisplayName("شركة التوصيل")]
        [Required(ErrorMessage = "حقل شركة التوصيل مطلوب.")]
        public int DeliveryCompanyId { get; set; }

        [ForeignKey("DeliveryCompanyId")]
        public DeliveryCompany? DeliveryCompany { get; set; }

        [DisplayName("شركة التصنيع")]
        [Required(ErrorMessage = "حقل شركة التصنيع مطلوب.")]

        public int? ManufacturingCompanyId { get; set; }

        [ForeignKey("ManufacturingCompanyId")]
        public ManufacturingCompany? ManufacturingCompany { get; set; }

        [DisplayName("تاريخ الإضافة")]
        [Required(ErrorMessage = "حقل تاريخ الإضافة مطلوب.")]

        public DateTime DateAdded { get; set; } = DateTime.Now; // Default to current date and time

        [DisplayName("تاريخ التحديث")]
        [Required(ErrorMessage = "حقل تاريخ التحديث مطلوب.")]

        public DateTime DateUpdated { get; set; }

        // Foreign key property
        [DisplayName(" المنتج الرئيسي")]
        [Required(ErrorMessage = "حقل تاريخ التحديث مطلوب.")]
        public int MainWarehouseId { get; set; }
        // Navigation property
        public MainWarehouse MainWarehouse { get; set; }

        public Common.Countries Countries { get; set; }

        public string? City { get; set; }

        public bool IsShown { get; set; } = true;

        public ICollection<OrderWarehouse> OrderWarehouses { get; set; }

        public ICollection<WarehouseEditHistory> WarehouseEditHistories { get; set; }


        [DisplayName("اسم القسم")]
        public int? SubWarehouseId { get; set; } // Foreign key for SubWarehouse

        // Navigation property
        [ForeignKey("SubWarehouseId")]
        public SubWarehouse? SubWarehouse { get; set; }

    }
}