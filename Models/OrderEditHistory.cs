using static lotus_blue.Models.Common;
using System;
using System.ComponentModel.DataAnnotations;
namespace lotus_blue.Models
{
    public class OrderEditHistory
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int OrderId { get; set; } // References the original Order ID
        [Required]
        public int EditNumber { get; set; } // Tracks the sequence of edits for an Order
        public Countries Country { get; set; }
        public string State { get; set; }
        public OrderSourceEnum OrderSource { get; set; }
        public string? SourceName { get; set; }
        public int? ManufacturingCompanyId { get; set; }
        public virtual ManufacturingCompany ManufacturingCompany { get; set; }
        public int DeliveryCompanyId { get; set; }
        public DeliveryCompany DeliveryCompany { get; set; }
        public string TelephoneNumber { get; set; }
        public string? SecondTelephoneNumber { get; set; }
        public string CustomerName { get; set; }
        public string? Notes { get; set; }
        public string Address { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastEditedDate { get; set; }
        public DateTime? FixedOrderDate { get; set; }
        public DateTime? InstantAddedDate { get; set; }
        public OrderStatusEnum OrderStatus { get; set; }
        public decimal TotalPrice { get; set; }
        public int? ExternalOrderId { get; set; }
        public string ApplicationUserId { get; set; }
        public string? ExternalShipmentCode { get; set; }
        public bool FromComments { get; set; }      
        public bool Gender { get; set; }
        public ICollection<OrderWarehouseEditHistory> OrderWarehouseEditHistories { get; set; }

        public bool IsPaid { get; set; }

        public string? Editedby { get; set; }

        public bool FromOffers { get; set; } = false;

        public int? CampaignId { get; set; }
        public virtual Campaign? Campaign { get; set; }

        public decimal DeliveryPrice { get; set; }

        public string? Chaturl { get; set; }

    }
}
