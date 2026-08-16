using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models.AppViewModel
{
    public class AppRatingViewmodel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int OrdersCount { get; set; }
        public int TotalProductsCount { get; set; }
        public int TotalOrdersWithWarehouseItemMoreThanOne { get; set; }
        public string TotalPriceUSD { get; set; }
        public string TotalPriceTRY { get; set; }
        public int QueenProductCount { get; set; }
        public int RoziProductCount { get; set; }
        public int PowderProductCount { get; set; }
        public int RoyalProductCount { get; set; }
        public int VilveltCreamProductCount { get; set; }
        public int PansiCreamProductCount { get; set; }
        public int BrushProductCount { get; set; }
        public int MascaraProductCount { get; set; }
        [DisplayFormat(DataFormatString = "{0:0.0}")]
        public string Rating { get; set; }
        public int FailedDeliveryOrdersCount { get; set; }
        public int DeliveredOrdersCount { get; set; }

        [DisplayFormat(DataFormatString = "{0:0.0}")]
        public decimal FailedDeliveryOrdersPriceUSD { get; set; }

        [DisplayFormat(DataFormatString = "{0:0.0}")]
        public decimal FailedDeliveryOrdersPriceTRY { get; set; }

        [DisplayFormat(DataFormatString = "{0:n0}")]
        public decimal DeliveredOrdersPriceUSD { get; set; }

        [DisplayFormat(DataFormatString = "{0:n0}")]
        public decimal DeliveredOrdersPriceTRY { get; set; }

        [DisplayFormat(DataFormatString = "{0:n0}")]
        public decimal FailedDeliverPercentage { get; set; }

        [DisplayFormat(DataFormatString = "{0:n0}")]
        public decimal DeliveredPercentage { get; set; }
        public int OrderFromComments { get; set; }
    }
}
