using CoffeeHouse.Application.DTOs.Accounts;
using CoffeeHouse.Application.DTOs.Staffs;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Domain.Entities;
using CoffeeHouse.Domain.Enums;
using CoffeeHouse.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace CoffeeHouse.Application.Services.Implementations
{
    public class AccountService : IAccountService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUnitOfWork _uow;
        private readonly ITokenService _tokenService;
        private readonly IMemoryCache _cache;
        private readonly IEmailService _emailService;


        public AccountService(UserManager<ApplicationUser> userManager, IUnitOfWork uow, ITokenService tokenService, IMemoryCache cache, IEmailService emailService)
        {
            _userManager = userManager;
            _uow = uow;
            _tokenService = tokenService;
            _cache = cache;
            _emailService = emailService;
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

        // =============================================
        // QUÊN MẬT KHẨU - Sinh OTP 6 số và lưu cache
        // =============================================
        public async Task<string> ForgotPasswordAsync(ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
            {
                throw new Exception("Nếu email tồn tại trong hệ thống, mã xác nhận đã được gửi.");
            }

            // Sinh mã OTP 6 số
            string otpCode = new Random().Next(100000, 999999).ToString();

            // Lưu vào MemoryCache, hết hạn sau 15 phút
            _cache.Set($"OTP_{user.Email}", otpCode, TimeSpan.FromMinutes(15));

            // ============================================
            // 📧 GỬI EMAIL THẬT CÓ GIAO DIỆN HTML ĐẸP MẮT
            // ============================================
            string subject = "🔑 Yêu cầu khôi phục mật khẩu - Coffee House";
            string htmlBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 10px; overflow: hidden;'>
                    <div style='background-color: #1e1e2d; padding: 20px; text-align: center;'>
                        <h2 style='color: #ffc107; margin: 0;'>COFFEE HOUSE</h2>
                    </div>
                    <div style='padding: 30px; background-color: #f9f9f9;'>
                        <p style='font-size: 16px; color: #333;'>Xin chào <strong>{user.FullName ?? user.UserName}</strong>,</p>
                        <p style='font-size: 16px; color: #333;'>Bạn vừa yêu cầu đặt lại mật khẩu cho tài khoản tại Coffee House. Dưới đây là mã xác nhận (OTP) của bạn:</p>
                        
                        <div style='text-align: center; margin: 30px 0;'>
                            <span style='font-size: 32px; font-weight: bold; color: #1e1e2d; letter-spacing: 5px; background-color: #ffc107; padding: 10px 20px; border-radius: 5px;'>
                                {otpCode}
                            </span>
                        </div>
                        
                        <p style='font-size: 14px; color: #dc3545; font-weight: bold;'>⚠️ Mã này chỉ có hiệu lực trong 15 phút.</p>
                        <p style='font-size: 14px; color: #666;'>Nếu bạn không yêu cầu thay đổi mật khẩu, vui lòng bỏ qua email này hoặc liên hệ quản trị viên.</p>
                    </div>
                </div>";

            // GỌI EMAIL SERVICE CHẠY BẤT ĐỒNG BỘ
            await _emailService.SendEmailAsync(user.Email, subject, htmlBody);

            return otpCode;
        }

        // =============================================
        // ĐẶT LẠI MẬT KHẨU - Xác thực OTP và đổi mật khẩu
        // =============================================
        public async Task<bool> ResetPasswordAsync(ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                throw new Exception("Email không tồn tại trong hệ thống.");

            // Kéo mã OTP từ Cache dựa vào Email
            if (!_cache.TryGetValue($"OTP_{dto.Email}", out string? cachedOtp))
            {
                throw new Exception("Mã xác nhận đã hết hạn. Vui lòng yêu cầu cấp lại mã mới.");
            }

            if (cachedOtp != dto.OtpCode)
            {
                throw new Exception("Mã xác nhận không chính xác.");
            }

            // Nếu đúng, sinh Reset Token ngầm qua Identity để hợp thức hóa đổi pass
            var identityResetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

            var result = await _userManager.ResetPasswordAsync(user, identityResetToken, dto.NewPassword);
            if (result.Succeeded)
            {
                // Thành công thì hủy luôn mã OTP
                _cache.Remove($"OTP_{dto.Email}");
                return true;
            }

            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new Exception($"Đặt lại mật khẩu thất bại: {errors}");
        }
    }
}
