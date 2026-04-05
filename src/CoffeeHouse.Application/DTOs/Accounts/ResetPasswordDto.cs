using System.ComponentModel.DataAnnotations;

namespace CoffeeHouse.Application.DTOs.Accounts
{
    public class ResetPasswordDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có đúng 6 ký tự.")]
        public string OtpCode { get; set; } = null!;

        [Required]
        [MinLength(6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.")]
        public string NewPassword { get; set; } = null!;
    }
}
