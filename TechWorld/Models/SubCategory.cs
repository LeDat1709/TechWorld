using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechWorld.Models
{
    public class SubCategory
    {
        public int SubCategoryID { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Tên danh mục phụ")]
        public string SubCategoryName { get; set; }

        [Display(Name = "Danh mục chính")]
        public int CategoryID { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("CategoryID")]
        public virtual Category Category { get; set; }

        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
