using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechWorld.Models
{
    public class ProductImage
    {
        [Key]
        public int ImageID { get; set; }

        public int? ProductID { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Đường dẫn ảnh")]
        public string ImagePath { get; set; }

        [Display(Name = "Ảnh chính")]
        public bool IsMainImage { get; set; } = false;

        // Navigation properties
        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; }
    }
}
