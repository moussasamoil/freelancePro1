using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lotus_blue.Models
{
    public class OrderPost
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public virtual Order Order { get; set; }

        public OrderPostType Type { get; set; }

        public string AuthorUserId { get; set; }
        public virtual ApplicationUser AuthorUser { get; set; }

        public string? Body { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual ICollection<OrderPostImage> Images { get; set; } = new List<OrderPostImage>();
    }
}
