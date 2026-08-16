using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace lotus_blue.Models
{
    public class OrderWarehouseEditHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }
        [JsonIgnore]
        public Order Order { get; set; }

        [Required]
        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; }

        public int Amount { get; set; }

        [Required]
        public DateTime EditDate { get; set; }

        [Required]
        public int EditNumber { get; set; } // Reflects the sequence of the edit

        // Link to the specific OrderEditHistory this change is part of
        public int OrderEditHistoryId { get; set; }
        public OrderEditHistory OrderEditHistory { get; set; }
    }
}
