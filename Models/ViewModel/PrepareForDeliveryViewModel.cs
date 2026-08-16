using System.Collections.Generic;
using lotus_blue.Models;

namespace lotus_blue.Models.ViewModel
{
    public class PrepareForDeliveryViewModel
    {
        public Order CurrentOrder { get; set; }
        public List<OrderWarehouse> OrderProducts { get; set; }
        public List<Order> OrderQueue { get; set; }
        public List<Order> RecentlySubmittedOrders { get; set; }

        // عدد الطلبات المتبقية في صفحة تنزيل الطلبات.
        // يستخدم لعرض رقم فقط على بادج زر "تنزيل الطلبات" بدل كلمة "جديد".
        public int RemainingDownloadCount { get; set; }
    }
}
