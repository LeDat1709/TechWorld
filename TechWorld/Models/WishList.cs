using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechWorld.Models
{
    [Table("WishListItems")]
    public class WishList
    {
            [Key]
            public int WishlistItemID { get; set; }

            [Required]
            public int UserID { get; set; } // dùng string nếu là User.Identity.Name

            [Required]
            public int ProductID { get; set; }

            public DateTime? AddedAt { get; set; }

            public Product Product { get; set; }
        
    }
}
