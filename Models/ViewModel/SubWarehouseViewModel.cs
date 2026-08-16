using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace lotus_blue.Models.ViewModel
{
    public class SubWarehouseViewModel
    {
        public int Id { get; set; }

        [DisplayName("اسم القسم")]
        [StringLength(255)]
        [Required(ErrorMessage = "حقل الاسم مطلوب.")]
        public string Name { get; set; }
        public string ProductCode { get; set; }
        public int? MainWarehouseId { get; set; }
        public MainWarehouseViewModel? MainWarehouse { get; set; }
    }
}
