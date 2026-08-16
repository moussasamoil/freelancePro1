namespace lotus_blue.Models.ViewModel
{
    public class HomeViewModel
    {

        public PaginationViewModel<OrderViewModel> PaginationViewModel { get; set; }
        public EmployeeRatingCompositeViewModel EmployeeRatingCompositeViewModel { get; set; }


        public List<ManufacturingCompanyViewModel> ManufacturingCompanies { get; set; }

        public List<OrderStatusEnum> OrderStatuses { get; set; } // Add this line

        public List<OrderStatusesForDeliveryCompanyAndRepresentativeEnum> OrderStatusesForDeliveryCompanyAndRepresentative { get; set; } // Add this line


        public List<OrderStatusesForEmployeesEnum> OrderStatusesForEmployees { get; set; }

        public List<OrderStatusesForFollowUpDepartmentEnum> OrderStatusesForFollowUpDepartment { get; set; }


        public List<DeliveryCompanyViewModel> DeliveryCompanies { get; set; }

        public List<DeliveryCompanyViewModel> DeliveryRepresentative { get; set; }


        public List<Common.Countries> Countries { get; set; }

        // display the user name 
        public string UserName { get; set; }

        //store name from sbs
        public string StoreNameFromSbS { get; set; }

        // get usernames for each order 
        public Dictionary<int, string> UserNamesForOrders { get; set; }

        // get dleivery company price for each order 
        public Dictionary<int, decimal> DeliveryCompanyPrices { get; set; }

        // Property to hold the total price for delivery companies

        // Property to hold the total price of orders
        public string TotalOrderPrice { get; set; }

        public string TotalOrderPriceDollar { get; set; }

        public string TotalOrderPriceTRY { get; set; }


        // single selected country 
        public string? SelectedCoutnry { get; set; }


        public Dictionary<int, string> DeliveryCompanyLogos { get; set; }
        public Dictionary<int, string> ManufacturingCompanyLogos { get; set; }





        // for the table 

        public Dictionary<string, string> CountryImageUrls { get; set; }

        public Dictionary<string, string> SocialMediaIconUrls { get; set; }

        public Dictionary<string, string> OrderStatusIconUrls { get; set; }

        public Dictionary<string, string> CurrencySymbols { get; set; }

        public bool ShowFailureReasonColumn { get; set; }

        public string? DebugQuery { get; set; }

        // CallCenter on Home with zero filters: table is capped to the user's latest order.
        public bool IsCallCenterNoFilters { get; set; }

        // CallCenter on Home (any state): page-size selector, pagination, and total-orders count are permanently hidden.
        public bool IsCallCenter { get; set; }

    }

  
    public class CountryInfo
    {
        public string CountryName { get; set; }
        public string ImageUrl { get; set; }
        // Add more properties if needed
    }
  




}
