using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechWorld.Models
{
    public class UserAddress
    {
        [Key]
        public int UserAddressID { get; set; }

        public int UserID { get; set; }

        [Required(ErrorMessage = "Địa chỉ chi tiết không được để trống.")]
        [StringLength(255)]
        [Display(Name = "Địa chỉ")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Tỉnh/Thành phố không được để trống.")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn Tỉnh/Thành phố.")]
        public int ProvinceID { get; set; }

        [Required(ErrorMessage = "Quận/Huyện không được để trống.")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn Quận/Huyện.")]
        public int DistrictID { get; set; }

        [Required(ErrorMessage = "Phường/Xã không được để trống.")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn Phường/Xã.")]
        public int WardID { get; set; }

        [Required(ErrorMessage = "Số điện thoại không được để trống.")]
        [StringLength(20)]
        [Display(Name = "Số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Đặt làm mặc định")]
        public bool IsDefault { get; set; } = false;

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime CreatedAt { get; set; }
        public DateTime AddedAt { get; set; }

        [ForeignKey("UserID")]
        public User User { get; set; }

        [ForeignKey("ProvinceID")]
        public Province Province { get; set; }

        [ForeignKey("DistrictID")]
        public District District { get; set; }

        [ForeignKey("WardID")]
        public Ward Ward { get; set; }

        public string FullAddressDisplay
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(Address)) parts.Add(Address);
                if (Ward != null && !string.IsNullOrEmpty(Ward.WardName)) parts.Add(Ward.WardName);
                if (District != null && !string.IsNullOrEmpty(District.DistrictName)) parts.Add(District.DistrictName);
                if (Province != null && !string.IsNullOrEmpty(Province.ProvinceName)) parts.Add(Province.ProvinceName);

                return string.Join(", ", parts);
            }
        }
    }
}
