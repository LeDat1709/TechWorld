using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechWorld.Models
{
    public class Promotion
    {
        public int PromotionID { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên chương trình khuyến mãi.")]
        [StringLength(100, ErrorMessage = "Tên khuyến mãi không được vượt quá 100 ký tự.")]
        [Display(Name = "Tên chương trình")]
        public string PromotionName { get; set; } // Thêm PromotionName

        [Display(Name = "Sản phẩm áp dụng")]
        public int? ProductID { get; set; } // Cho phép null nếu là KM chung

        [Required(ErrorMessage = "Vui lòng nhập phần trăm giảm giá.")]
        [Range(0.00, 100.00, ErrorMessage = "Phần trăm giảm giá phải từ 0 đến 100.")]
        [Column(TypeName = "decimal(5, 2)")]
        [Display(Name = "Phần trăm giảm (%)")]
        public decimal DiscountPercentage { get; set; }

        [Display(Name = "Kích hoạt")]
        public bool IsActive { get; set; } = true; // Thêm IsActive, mặc định là true

        [StringLength(20, ErrorMessage = "Mã khuyến mãi không được vượt quá 20 ký tự.")]
        [RegularExpression(@"^[a-zA-Z0-9]*$", ErrorMessage = "Mã khuyến mãi chỉ chứa chữ cái và số.")]
        [Display(Name = "Mã khuyến mãi (Promo Code)")]
        public string? PromoCode { get; set; } // Thêm PromoCode, có thể là null

        [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu.")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Ngày bắt đầu")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc.")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Ngày kết thúc")]
        // Thêm validation để đảm bảo EndDate > StartDate (cần custom validation hoặc check ở Controller)
        public DateTime EndDate { get; set; }

        [StringLength(255, ErrorMessage = "Mô tả không được vượt quá 255 ký tự.")]
        [Display(Name = "Mô tả")]
        public string Description { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "Số tiền giảm")]
        public decimal? DiscountAmount { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "Giá trị đơn hàng tối thiểu")]
        public decimal? MinOrderValue { get; set; }

        public int? RankID { get; set; } = null;

        // Navigation property
        [ForeignKey("ProductID")]
        public virtual Product? Product { get; set; }
        public virtual Rank? Rank { get; set; }

        // Custom validation (ví dụ, có thể đặt ở đây hoặc check trong Controller)
        // public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        // {
        //     if (EndDate <= StartDate)
        //     {
        //         yield return new ValidationResult(
        //             "Ngày kết thúc phải sau ngày bắt đầu.",
        //             new[] { nameof(EndDate) });
        //     }
        // }
    }
}
