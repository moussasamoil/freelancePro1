namespace lotus_blue.Models.ViewModel
{
    public class OrderShippingDetailsApiViewModel
    {
        public List<WarehouseDetailViewModel> WarehouseDetails { get; set; }
        public List<OrderStatusHistoryViewModel> OrderStatusHistory { get; set; }
        public string City { get; set; }
        public string Country { get; set; }

        public int OrderId { get; set; }
    }

    public class WarehouseDetailViewModel
    {
        public string WarehouseName { get; set; }
        public string ImageUrl { get; set; }
        public int Amount { get; set; }
    }

    public class OrderStatusHistoryViewModel
    {
        public string CreatedAt { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
    }

    public class WarehouseResponse
    {
        public List<WarehouseDetailViewModelFromApi> Warehouses { get; set; }
    }

    public class WarehouseDetailViewModelFromApi
    {
        public string warehouseName { get; set; }
        public string imageUrl { get; set; }
        public int amount { get; set; }
    }
}
