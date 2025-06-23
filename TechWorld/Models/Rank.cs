using Microsoft.Build.Framework;

namespace TechWorld.Models
{
    public class Rank
    {
        public int RankID { get; set; }

        [Required]
        public string RankName { get; set; }

        public string Description { get; set; }

        public int MinimumPoints { get; set; }

        public decimal DiscountPercentage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public ICollection<User> Users { get; set; }
    }
}
