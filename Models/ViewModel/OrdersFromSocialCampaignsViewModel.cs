namespace lotus_blue.Models.ViewModel
{
    public class OrdersFromSocialCampaignsViewModel
    {
        public int OrderId { get; set; } // رقم الوصل
        public string CustomerName { get; set; } // الاسم
        public string PhoneNumber { get; set; } // رقم الهاتف
        public string OrderStatus { get; set; } // حالة الطلب
        public DateTime Date { get; set; } // التاريخ
        public string Country { get; set; } // الدولة
        public string SourceName { get; set; } // اسم الصفحة

        public string OrderSource { get; set; } // order source from social media 

        public string City { get; set; } // المدينة
        public int? ManufacturingCompanyId { get; set; } // الشركة المصنعة

        public string ManufacturingCompanyName { get; set; } // الشركة المصنعة
        public string DeliveryCompanyName { get; set; } // شركة التوصيل
        public int Amount { get; set; } // قيمة المبلغ

        public Dictionary<int, string> UserNamesForOrders { get; set; } // user names for orders 

        public string StoreNameFromSbs { get; set; } // store name 


        public Dictionary<int, string> DeliveryCompanyLogos { get; set; }
        public Dictionary<int, string> ManufacturingCompanyLogos { get; set; }
    }

}
