using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Accounts;
using CoffeeHouse.Application.DTOs.Staffs;
using CoffeeHouse.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CoffeeHouse.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountService _accountService;
        public AccountsController(IAccountService accountService) => _accountService = accountService;

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var result = await _accountService.RegisterAsync(dto);
            if (result) return Ok(ApiResponse<string>.SuccessResult("Đăng ký tài khoản thành công!"));

            return BadRequest(ApiResponse<string>.FailureResult("Đăng ký thất bại. Email có thể đã tồn tại."));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var token = await _accountService.LoginAsync(dto);

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(ApiResponse<string>.FailureResult("Email hoặc mật khẩu không chính xác."));
            }

            // Trả về Token cho Frontend lưu vào LocalStorage/Cookie
            return Ok(ApiResponse<string>.SuccessResult(token, "Đăng nhập thành công!"));
        }

        [HttpPost("create-staff")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateStaff([FromBody] CreateStaffDto dto)
        {
            try
            {
                var result = await _accountService.CreateStaffAccountAsync(dto);
                if (result)
                {
                    return Ok(new { message = $"Tạo tài khoản nhân viên ({dto.Role}) thành công!" });
                }
                return BadRequest(new { message = "Tạo tài khoản thất bại." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            try
            {
                var encodedToken = await _accountService.ForgotPasswordAsync(dto);

                // Trả token về cho Frontend (Dùng để TEST, Production thì chỉ gửi qua Email)
                return Ok(ApiResponse<string>.SuccessResult(encodedToken, "Mã xác nhận đã được gửi đến email của bạn."));
            }
            catch (Exception ex)
            {
                // Vẫn trả Ok để tránh lộ email tồn tại hay không
                return Ok(ApiResponse<string>.SuccessResult(null!, ex.Message));
            }
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            try
            {
                await _accountService.ResetPasswordAsync(dto);
                return Ok(ApiResponse<string>.SuccessResult("Đổi mật khẩu thành công! Vui lòng đăng nhập lại.", "Thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.FailureResult(ex.Message));
            }
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                    return Unauthorized(ApiResponse<string>.FailureResult("Không xác định được danh tính."));

                var profile = await _accountService.GetProfileAsync(userId);
                return Ok(ApiResponse<UserProfileDto>.SuccessResult(profile));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.FailureResult(ex.Message));
            }
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                    return Unauthorized(ApiResponse<string>.FailureResult("Không xác định được danh tính."));

                var success = await _accountService.UpdateProfileAsync(userId, dto);
                if (success)
                    return Ok(ApiResponse<string>.SuccessResult("Cập nhật thông tin thành công."));
                
                return BadRequest(ApiResponse<string>.FailureResult("Cập nhật thất bại."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.FailureResult(ex.Message));
            }
        }

        [HttpPut("profile/change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                    return Unauthorized(ApiResponse<string>.FailureResult("Không xác định được danh tính."));

                var success = await _accountService.ChangePasswordAsync(userId, dto);
                if (success)
                    return Ok(ApiResponse<string>.SuccessResult("Đổi mật khẩu thành công."));

                return BadRequest(ApiResponse<string>.FailureResult("Đổi mật khẩu thất bại."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.FailureResult(ex.Message));
            }
        }
    }
}
