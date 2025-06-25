using System.ComponentModel.DataAnnotations;

namespace TechWorld.Models
{
    public class PaymentMethod
    {
        public int PaymentMethodID { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Phương thức thanh toán")]
        public string MethodName { get; set; }

        [Display(Name = "Trạng thái")]
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<Order>? Orders { get; set; }
    }
}
