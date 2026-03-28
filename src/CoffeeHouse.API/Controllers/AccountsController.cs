using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Accounts;
using CoffeeHouse.Application.DTOs.Staffs;
using CoffeeHouse.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    }
}
