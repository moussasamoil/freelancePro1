
    namespace lotus_blue.Models.ViewModel
    {
        public class FinancialDeliveryCompanyViewModel
        {
            public int? DeliveryCompanyId { get; set; }

            public GetDataListViewModel DeliveryCompany { get; set; }

            // حسابات انتظار التجهيز 
            public string PendingTotalPrice { get; set; } = "";
            public int? PendingOrdersCount { get; set; } = 0;


            //تم التسليم 
            public string DeliveredTotalPrice { get; set; } = "";
            public int? DeliveredOrdersCount { get; set; } = 0;

            // تم التسليم 
            public string DeliveredTotalDelvieryCompanyPrice { get; set; } = "";


            // الطلبات المرجعة 
            public string ReturnedTotalPrice { get; set; } = "";
            public int? ReturnedOrdersCount { get; set; } = 0;


            // اخطاء المندوبين
            public string AssignedTotalPrice { get; set; } = "";
            public int? AssignedOrdersCount { get; set; } = 0;


            //الطلبات المؤجلة 
            public string UnderProcessTotalPrice { get; set; } = "";
            public int? UnderProcessOrdersCount { get; set; } = 0;

            public string TotalOrderPirce { get; set; } = "";

            // جمع جميع الطلبات
            public int? TotalOrdersCount { get; set; } = 0;

            //null
            public int? CurrentAccountCount { get; set; } = 0;

            //حساب جاري للشركة 
            public string OnGoingAccountPrice { get; set; } = "";
            public int? OnGoingAccountCount { get; set; } = 0;


            // حساب الجاري لشركة التوصيل 

            public string OnGoingAccountDeliveryCompanyPrice { get; set; } = "";

            public string OnGoingAccountDeliveryCompanyPriceDollar { get; set; } = "";

            // حساب طلبات قيد التحصيل 

            public string deferredTotalPrice { get; set; } = "";
            public int? deferredOrdersCount { get; set; } = 0;


            //حسابات طلب قيد التحصيل للشركة التوصيل
            public string deferredTotalDeliveryCompanyPrice { get; set; } = "";


            // حساب المبلغ المدفوع
            public string PaidOrdersPrice { get; set; } = "";
            public int? PaidOrdersCount { get; set; } = 0;


            // حسابات المبلغ المدفوع من شركة التوصيل 

            public string PaidOrdersDeliveryCompanyPrice { get; set; } = "";

            public string PaidOrdersDeliveryCompanyPriceDollar { get; set; } = "";

            public string Currency { get; set; } = "";

            // حسابات مبلغ مؤجل
            public string PostponedOrdersPrice { get; set; } = "";
            public int? PostponedOrdersCount { get; set; } = 0;

            // total difference

            // ongoing account differene

            //الحساب الجاري
            public string OnGoingAccountDifference { get; set; } = "";
            // قيج التحصيل
            public string deferredDifference { get; set; } = "";
            // المبلغ المدفوع
            public string PaidDifference { get; set; } = "";
            // تم التسليم
            public string DeliveredDifference { get; set; } = "";

            public List<OrderReportViewModel> OrderReports { get; set; }

            // delviey company logo url 
            public string LogoUrl { get; set; } = ""; // Add this line

        }
    
    
}
