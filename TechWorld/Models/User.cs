using System.ComponentModel.DataAnnotations;

namespace TechWorld.Models
{
    public class User
    {
        public User()
        {
            // Khởi tạo các collection để tránh lỗi null reference
            Orders = new HashSet<Order>();
            Reviews = new HashSet<Review>();
        }

        public int UserID { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Tên đăng nhập")]
        public string UserName { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Họ tên")]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [StringLength(255)]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [StringLength(15)]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }

        [StringLength(255)]
        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Display(Name = "Vai trò")]
        public string? Role { get; set; } = "Customer";

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<Order> Orders { get; set; }
        public virtual ICollection<Review> Reviews { get; set; }
        public virtual ICollection<UserAddress>? UserAddresses { get; set; }

        public int? Points { get; set; } = 0;

        public int? RankID { get; set; }

        public Rank? Rank { get; set; }
        //public string? ResetToken { get; set; } // Token dùng để đặt lại mật khẩu
        //public DateTime? ResetTokenExpiry { get; set; } // Thời gian hết hạn của token

        public bool IsTwoFactorEnabled { get; set; }
    }
}
