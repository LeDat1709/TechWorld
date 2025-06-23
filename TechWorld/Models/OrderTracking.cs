using System.ComponentModel.DataAnnotations;

namespace TechWorld.Models
{
    public class OrderTracking
    {
        [Required(ErrorMessage = "Vui lòng nhập mã đơn hàng")]
        [Display(Name = "Mã đơn hàng")]
        public string OrderNumber { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email đặt hàng")]
        public string Email { get; set; }
    }
}
