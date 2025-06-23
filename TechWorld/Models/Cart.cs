using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechWorld.Models
{
    public class Cart // Đây là DB entity cho giỏ hàng
    {
        [Key] // Đánh dấu CartID là khóa chính
        public int CartID { get; set; }

        public int UserID { get; set; }
        [ForeignKey("UserID")] // Khóa ngoại tới bảng Users
        public User User { get; set; } // Navigation property tới model người dùng của bạn (giả sử là ApplicationUser)

        public DateTime CreatedAt { get; set; } = DateTime.Now; // Thời gian tạo giỏ hàng
        public DateTime LastUpdatedAt { get; set; } = DateTime.Now; // Thời gian cập nhật cuối cùng

        // Collection của các mục trong giỏ hàng (DB entities)
        // Tên của collection này phải là CartItem (DB Entity)
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    }
}
