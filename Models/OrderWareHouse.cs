using System.Text.Json.Serialization;

namespace lotus_blue.Models
{
    public class OrderWarehouse
    {
        public int OrderId { get; set; }
        [JsonIgnore]
        public Order Order { get; set; }

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; }

        public int Amount { get; set; }
    }
}
