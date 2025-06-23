using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechWorld.Models
{
    public class Order
    {
        public Order()
        {
            // Khởi tạo các collection để tránh lỗi null reference
            OrderDetails = new HashSet<OrderDetail>();
            OrderStatuses = new HashSet<OrderStatus>();
        }

        [Key]
        public int OrderID { get; set; }

        [StringLength(20)]
        [Display(Name = "Mã đơn hàng")]
        public string? OrderNumber { get; set; }

        public int? UserID { get; set; }

        [DataType(DataType.Currency)]
        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "Tổng tiền")]
        public decimal? TotalAmount { get; set; }

        [Display(Name = "Giảm giá")]
        public decimal? Discount { get; set; }
        public int? PaymentMethodID { get; set; }

        [StringLength(50)]
        [Display(Name = "Trạng thái đơn hàng")]
        public string OrderStatus { get; set; } = "Chờ xác nhận";

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Ngày cập nhật")]
        public DateTime? UpdatedAt { get; set; }

        [StringLength(255)]
        [Display(Name = "Địa chỉ giao hàng")]
        public string ShippingAddress { get; set; }

        [StringLength(500)]
        [Display(Name = "Ghi chú")]
        public string? Notes { get; set; }

        // Navigation properties
        [ForeignKey("UserID")]
        public virtual User? User { get; set; }

        [ForeignKey("PaymentMethodID")]
        public virtual PaymentMethod? PaymentMethod { get; set; }

        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
        public virtual ICollection<OrderStatus> OrderStatuses { get; set; }

        // Thuộc tính không ánh xạ vào cơ sở dữ liệu
        [NotMapped]
        [Display(Name = "Họ tên")]
        public string FullName => User?.FullName ?? "Khách vãng lai";

        [NotMapped]
        [Display(Name = "Email")]
        public string Email => User?.Email ?? "unknown@example.com";

        [NotMapped]
        [Display(Name = "Số điện thoại")]
        public string Phone => User?.Phone ?? "Không có";
    }
}
