using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechWorld.Models
{
    public class District
    {
        [Key]
        public int DistrictID { get; set; }

        public int ProvinceID { get; set; }

        [Required]
        [StringLength(100)]
        public string DistrictName { get; set; }

        [ForeignKey("ProvinceID")]
        public Province Province { get; set; }
        public ICollection<Ward> Wards { get; set; }
    }
}
