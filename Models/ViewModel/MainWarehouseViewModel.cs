using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace lotus_blue.Models
{
    public class MainWarehouseViewModel
    {
        public int Id { get; set; }

        [DisplayName("الاسم")]
        [StringLength(255, ErrorMessage = "الاسم لا يمكن أن يكون أطول من 255 حرف.")]
        [Required(ErrorMessage = "حقل الاسم مطلوب.")]
        public string Name { get; set; }

        [DisplayName("URL الصورة")]
        public string? ImageUrl { get; set; }

    }
}
