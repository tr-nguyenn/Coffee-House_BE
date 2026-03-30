using CoffeeHouse.Application.DTOs.Accounts;
using CoffeeHouse.Application.DTOs.Staffs;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Domain.Entities;
using CoffeeHouse.Domain.Enums;
using CoffeeHouse.Infrastructure;
using Microsoft.AspNetCore.Identity;

namespace CoffeeHouse.Application.Services.Implementations
{
    public class AccountService : IAccountService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUnitOfWork _uow;
        private readonly ITokenService _tokenService;


        public AccountService(UserManager<ApplicationUser> userManager, IUnitOfWork uow, ITokenService tokenService )
        {
            _userManager = userManager;
            _uow = uow;
            _tokenService = tokenService;
        }

        public async Task<bool> RegisterAsync(RegisterDto dto)
        {
            var appUser = new ApplicationUser
            {
                UserName = dto.UserName,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber
            };

            var result = await _userManager.CreateAsync(appUser, dto.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(appUser, "Customer");
                var domainUser = new Customer
                {
                    IdentityId = appUser.Id.ToString(),
                    FullName = dto.FullName,
                    PhoneNumber = dto.PhoneNumber,
                    RewardPoints = 0,
                };

                await _uow.Repository<Customer>().AddAsync(domainUser);
                await _uow.SaveChangesAsync();
                return true;
            }

            return false;
        }

        public async Task<string?> LoginAsync(LoginDto dto)
        {
            ApplicationUser? user = null;

            if (dto.UserNameOrEmail.Contains("@"))
            {
                user = await _userManager.FindByEmailAsync(dto.UserNameOrEmail);
            }
            else
            {
                user = await _userManager.FindByNameAsync(dto.UserNameOrEmail);
            }
            // Nếu không tìm thấy ai trong Database
            if (user == null) return null;

            // 2. Kiểm tra mật khẩu (UserManager tự lo việc giải mã và so sánh)
            var result = await _userManager.CheckPasswordAsync(user, dto.Password);

            if (result)
            {
                // 👉 1. ĐI LẤY ROLE TỪ DATABASE LÊN
                var roles = await _userManager.GetRolesAsync(user);

                // 👉 2. TRUYỀN ROLE VÀO HÀM TẠO TOKEN
                return _tokenService.CreateToken(user, roles);
            }

            return null;
        }

        
        public async Task<bool> CreateStaffAccountAsync(CreateStaffDto dto)
        {
            // 1. Tạo thực thể Identity
            var appUser = new ApplicationUser
            {
                UserName = dto.UserName,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                FullName = dto.FullName
            };

            var result = await _userManager.CreateAsync(appUser, dto.Password);

            if (result.Succeeded)
            {
                // 2. Ép vào cái Role mà Admin chỉ định (Thay vì mặc định là Customer)
                await _userManager.AddToRoleAsync(appUser, dto.Role);

                // 3. LƯU VÀO BẢNG STAFF (KHÔNG PHẢI BẢNG USER)
                var staff = new Staff
                {
                    IdentityId = appUser.Id.ToString(),
                    FullName = dto.FullName,
                    Status = StaffStatus.Active,
                    HireDate = DateTime.UtcNow
                };

                await _uow.Repository<Staff>().AddAsync(staff);
                await _uow.SaveChangesAsync();
                return true;
            }

            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new Exception($"Lỗi tạo tài khoản nhân viên: {errors}");
        }
    }
}
