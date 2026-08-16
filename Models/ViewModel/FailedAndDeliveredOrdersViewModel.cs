namespace lotus_blue.Models.ViewModel
{
    public class FailedAndDeliveredOrdersViewModel
    {
        public List<OrderCountByCountry> DeliveredOrdersByCountry { get; set; }
        public List<OrderCountByCountry> FailedOrdersByCountry { get; set; }
        public List<OrderCountByStore> DeliveredOrdersByStore { get; set; }
        public List<OrderCountByStore> FailedOrdersByStore { get; set; }
        
        public string TotalDeliveredOrdersPriceUSD { get; set; }
        public string TotalFailedOrdersPriceUSD { get; set; }
        public string TotalDeliveredOrdersPriceTL { get; set; }
        public string TotalFailedOrdersPriceTL { get; set; }
        public string DeliveredPercentage { get; set; }
        public string FailedPercentage { get; set; }

        public int TotalDeliveredOrders { get; set; }

        public int TotalFailedOrders { get; set; }


    }
}
