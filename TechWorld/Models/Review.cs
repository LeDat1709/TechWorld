using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechWorld.Models
{
    public class Review
    {
        public int ReviewID { get; set; }

        public int? ProductID { get; set; }

        public int? UserID { get; set; }

        [Required]
        [Range(1, 5)]
        [Display(Name = "Đánh giá")]
        public int Rating { get; set; }

        [Display(Name = "Bình luận")]
        public string Comment { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; }

        [ForeignKey("UserID")]
        public virtual User User { get; set; }
    }
}
