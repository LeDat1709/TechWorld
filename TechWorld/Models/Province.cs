using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechWorld.Models
{
    public class Province
    {
        [Key]
        public int ProvinceID { get; set; }

        [Required]
        [StringLength(100)]
        public string ProvinceName { get; set; }

        public ICollection<District> Districts { get; set; }
    }
}