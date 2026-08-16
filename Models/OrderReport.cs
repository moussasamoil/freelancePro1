using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lotus_blue.Models
{
    public class OrderReport
    {
        public int Id { get; set; }

        [Display(Name = "التاريخ")]
        public DateTime GeneratedTime { get; set; }

        [Display(Name = "قيمة المبلغ")]
        public decimal TotalAmount { get; set; }

        [Display(Name = "الدولة")]
        public Common.Countries? Country { get; set; }

        [Display(Name = "شركة التوصيل")]
        public int? DeliveryCompanyId { get; set; }
        public virtual DeliveryCompany DeliveryCompany { get; set; }

        [Display(Name = "حالة الطلب")]
        public OrderStatusEnum OrderStatus { get; set; }

        // Navigation property for orders
        public virtual ICollection<Order> Orders { get; set; }

        // NotMapped property for storing order IDs
        [NotMapped]
        public List<int> OrderIds { get; set; }

        public virtual ICollection<OrderReportOrder> OrderReportOrders { get; set; }

    }
}
