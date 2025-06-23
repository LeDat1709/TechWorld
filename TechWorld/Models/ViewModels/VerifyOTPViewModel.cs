using System.ComponentModel.DataAnnotations;

namespace TechWorld.Models.ViewModels
{
    public class VerifyOTPViewModel
    {
        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mã OTP là bắt buộc")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có đúng 6 ký tự")]
        public string OTP { get; set; }
    }
}
