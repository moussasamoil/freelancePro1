using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using Z.Expressions.Compiler;

namespace lotus_blue.Models.ViewModel
{
    public class RatingViewModel
    {
        // employe id 
        public string Id { get; set; }

        // employe name
        public string Name { get; set; }

        public string Image { get; set; }

        // employe totalorder count
        public int OrdersCount { get; set; }

        // employe fixed  totalorder count
        public int FixedOrdersCount { get; set; }

        // employe total prictcount for all orders 
        public int TotalProductsCount { get; set; }

        // total price for order without deliverycompanyprices
        public decimal TotalPrice { get; set; }

        // passing orders number with more than 1 product 
        public int TotalOrdersWithWarehouseItemMoreThanOne { get; set; }

        // total order price usd 
        public string TotalPriceUSD { get; set; }

        // total order price try
        public string TotalPriceTRY { get; set; }


        // rating for an employe 
        public string Rating { get; set; }


        // number of failed delivery orders

        public int FailedDeliveryOrdersCount { get; set; }

        // number of delived order s

        public int DeliveredOrdersCount { get; set; }


        // perentage of failed delivery orders

        public string FailedDeliverPercentage { get; set; }

        // perentage of delived order s

        public string DeliveredPercentage { get; set; }


        // faild order price dollar and tl 

        public string FailedDeliveryOrdersPriceUSD { get; set; }
        public string FailedDeliveryOrdersPriceTRY { get; set; }

        // deliverd order price dollar and tl 

        public string DeliveredOrdersPriceUSD { get; set; }

        public string DeliveredOrdersPriceTRY { get; set; }

        // order from comments count
        public int OrderFromComments { get; set; }

        public int OrderFromOffers { get; set; }


        public int OrderFromMales { get; set; }

        public int OrderFromFemales { get; set; }

        public int NumberOfBonuses { get; set; }

        public List<OrderCountByCountry> OrderCountsByCountry { get; set; } // New property for order counts by country

        public int PotentialOrdersCount { get; set; }

    }


    public class MainWarehouseOrderModel
    {
        public string MainWarehouseName { get; set; }
        public int TotalAmount { get; set; }
    }


    public class OrderCountByCountry
    {
        public string Country { get; set; }
        public int CountryId { get; set; }
        public int OrderCount { get; set; }
        public string TotalPriceWithoutDeliveryTl { get; set; }
        public string TotalPriceWithoutDeliveryUSD { get; set; }
        public string TotalPriceWithoutDeliverylocal { get; set; }
        public decimal Rating { get; set; } // Add the Rating property
        public string FixedOrderCount { get; set; }
        public string OrderFromCommentsCount { get; set;}
        public int OrderFromOffersCount { get; set; }
        public string OrderFromMalesCount { get; set; }
        public string OrderFromFemalesCount { get; set; }
        public int NumberOfBonuses { get; set; }

        public List<MainWarehouseOrderModel> MainWarehouseOrders { get; set; }

        public int TotalProductsCount { get; set; } // Add the TotalProductsCount property

        public int PotentialOrdersCount { get; set; }

    }

    public class OrderCountByStore
    {
        public string Name { get; set; }
        public int? StoreId { get; set; }
        public string StoreImage { get; set; }
        public int OrderCount { get; set; }
        public int OrderFromOffersCount { get; set; }
        public int NumberOfBonuses { get; set; }
        public int OrderFromCommentsCount { get; set; }
        public string TotalPriceWithoutDeliveryTl { get; set; }
        public string TotalPriceWithoutDeliveryUSD { get; set; }
        public decimal Rating { get; set; } // Add the Rating property

        public string OrderFromMalesCount { get; set; }
        public string OrderFromFemalesCount { get; set; }

        public string FixedOrderCount { get; set; }

        public int PotentialOrdersCount { get; set; }

    }

    public class EmployeeRatingCompositeViewModel
    {
        public List<RatingViewModel> EmployeeRatings { get; set; }
        public List<OrderCountByCountry> OrdersByCountry { get; set; }
        public List<OrderCountByStore> OrdersByStore { get; set; }

        public string TotalNumberOfOrders { get; set; }
        public string OrderFromCommentsCount { get; set; }
        public string OrderFromOfferDiscountsCount { get; set; }
        public int NumberOfBouneses { get; set; }

        public string OrderFromMalesCount { get; set; }
        public string OrderFromFemalesCount { get; set; }
        public string FixedOrdersCount { get; set; }
        public string OrderFromOffersCount { get; set; }
        public string TotalOrdersPriceInDollar { get; set; }
        public string TotalOrdersPriceInTL { get; set; }
        public string TotalProductsCount { get; set; } // Add this property

        public string PotentialOrdersCount { get; set; }

    }

}
