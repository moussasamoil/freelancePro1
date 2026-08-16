using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace lotus_blue.Models
{
    public class SubWarehouse
    {
        public int Id { get; set; }

        [DisplayName("اسم القسم")]
        [StringLength(255)]
        [Required(ErrorMessage = "حقل الاسم مطلوب.")]
        public string? Name { get; set; }

        public int? MainWarehouseId { get; set; }    
        public MainWarehouse? MainWarehouse { get; set; }

        public string? ProductCode { get; set; }

    }
}
