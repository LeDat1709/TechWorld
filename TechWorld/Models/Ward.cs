using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechWorld.Models
{
    public class Ward
    {
        [Key]
        public int WardID { get; set; }

        public int DistrictID { get; set; }

        [Required]
        [StringLength(100)]
        public string WardName { get; set; }

        [ForeignKey("DistrictID")]
        public District District { get; set; }
    }
}
