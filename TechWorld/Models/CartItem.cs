using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechWorld.Models
{
    public class CartItem // Đây là DB entity cho mục trong giỏ hàng
    {
        [Key] // Đánh dấu CartItemID là khóa chính
        public int CartItemID { get; set; }

        public int CartID { get; set; }
        [ForeignKey("CartID")] // Khóa ngoại tới bảng Carts
        public Cart Cart { get; set; } // Navigation property tới Cart (DB Entity)

        public int ProductID { get; set; }
        [ForeignKey("ProductID")] // Khóa ngoại tới bảng Products
        public Product Product { get; set; } // Navigation property tới Product (DB Entity)

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18, 2)")] // Kiểu dữ liệu tương ứng trong SQL
        public decimal UnitPriceAtAddition { get; set; } // Giá tại thời điểm thêm vào giỏ

        [Column(TypeName = "decimal(5, 2)")] // Kiểu dữ liệu tương ứng trong SQL
        public decimal DiscountPercentageAtAddition { get; set; } // % giảm giá tại thời điểm thêm vào giỏ
    }
}
