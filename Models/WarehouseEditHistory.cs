using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
using System; // Ensure you have this using directive for DateTime

namespace lotus_blue.Models
{
    public class WarehouseEditHistory
    {
        public int Id { get; set; }

        public int WarehouseId { get; set; }

        [ForeignKey("WarehouseId")]
        public Warehouse Warehouse { get; set; }

        [DisplayName("تاريخ التعديل")]
        public DateTime EditDate { get; set; }

        [DisplayName("تمت الإضافة")]
        public int AddedAmount { get; set; }

        [DisplayName("المستخدم")]
        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
    }
}
