using System.ComponentModel.DataAnnotations;

namespace CoffeeHouse.Application.DTOs.Accounts
{
    public class UserProfileDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int? RewardPoints { get; set; } // Chỉ hiển thị nếu là Customer
        public DateTime? HireDate { get; set; } // Chỉ hiển thị nếu là Staff
    }

    public class UpdateProfileDto
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên phải từ 2 đến 100 ký tự.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [RegularExpression(@"^(03|05|07|08|09)\d{8}$", ErrorMessage = "Số điện thoại không hợp lệ (Bắt đầu bằng 03, 05, 07, 08, 09 và gồm 10 chữ số)")]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.")]
        public string NewPassword { get; set; } = string.Empty;
    }
}
