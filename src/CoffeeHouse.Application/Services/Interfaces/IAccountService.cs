using CoffeeHouse.Application.DTOs.Accounts;
using CoffeeHouse.Application.DTOs.Staffs;

namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface IAccountService
    {
        Task<bool> RegisterAsync(RegisterDto dto);
        Task<string?> LoginAsync(LoginDto dto);
        Task<bool> CreateStaffAccountAsync(CreateStaffDto dto);
        Task<string> ForgotPasswordAsync(ForgotPasswordDto dto);
        Task<bool> ResetPasswordAsync(ResetPasswordDto dto);
        Task<UserProfileDto> GetProfileAsync(Guid userId);
        Task<bool> UpdateProfileAsync(Guid userId, UpdateProfileDto dto);
        Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto dto);
    }
}
