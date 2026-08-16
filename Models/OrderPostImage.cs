using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models
{
    public class OrderPostImage
    {
        public int Id { get; set; }

        public int OrderPostId { get; set; }
        public virtual OrderPost OrderPost { get; set; }

        [StringLength(512)]
        public string Url { get; set; }

        public int SortOrder { get; set; }
    }
}
