using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace lotus_blue.Models
{
    public class MainWarehouse
    {
        public int Id { get; set; }

        [DisplayName("الاسم")]
        [StringLength(255)]
        [Required(ErrorMessage = "حقل الاسم مطلوب.")]
        public string Name { get; set; }

        public string ImageUrl { get; set; }
    }
}
