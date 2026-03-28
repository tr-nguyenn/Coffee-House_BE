using AutoMapper;
using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Users;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Domain.Entities;
using CoffeeHouse.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CoffeeHouse.Application.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserService(IUnitOfWork unitOfWork, IMapper mapper, UserManager<ApplicationUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _userManager = userManager;
        }

        public async Task<PagedResult<UserDto>> GetAllPagedAsync(UserFilterDto filterDto)
        {
            var searchTerm = filterDto.Search?.Trim().ToLower();
            Expression<Func<Customer, bool>>? filter = null;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                filter = u => u.FullName.ToLower().Contains(searchTerm) ||
                              u.PhoneNumber.Contains(searchTerm);
            }

            // Gọi Repository của bảng Customer (Domain)
            var result = await _unitOfWork.Repository<Customer>().GetAllPagedAsync(
                pageNumber: filterDto.PageNumber,
                pageSize: filterDto.PageSize,
                filter: filter,
                orderBy: q => q.OrderByDescending(u => u.CreatedAt)
            );

            return new PagedResult<UserDto>
            {
                Items = _mapper.Map<List<UserDto>>(result.Items),
                TotalCount = result.TotalCount,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize
            };
        }

        public async Task<UserDto?> GetByIdAsync(Guid id)
        {
            var user = await _unitOfWork.Repository<Customer>().GetByIdAsync(id);
            return _mapper.Map<UserDto>(user);
        }

        public async Task<UserDto> CreateAsync(CreateUserDto dto)
        {
            // 1. Kiểm tra SĐT đã tồn tại bên Domain chưa
            var existing = await _unitOfWork.Repository<Customer>().GetAllPagedAsync(filter: u => u.PhoneNumber == dto.PhoneNumber);
            if (existing.TotalCount > 0) throw new Exception("Số điện thoại ni đã đăng ký thẻ thành viên rồi.");

            // 2. TẠO TÀI KHOẢN ĐĂNG NHẬP (IDENTITY)
            var appUser = new ApplicationUser
            {
                UserName = dto.PhoneNumber, // Dùng SĐT làm tên đăng nhập
                PhoneNumber = dto.PhoneNumber,
                FullName = dto.FullName,
                Email = $"{dto.PhoneNumber}@khachhang.coffee" // Email giả để pass validation
            };

            var result = await _userManager.CreateAsync(appUser, "Coffee@123"); // Pass mặc định
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new Exception($"Lỗi tạo tài khoản đăng nhập: {errors}");
            }

            // Gắn quyền Khách hàng
            await _userManager.AddToRoleAsync(appUser, "Customer");

            // 3. TẠO HỒ SƠ KHÁCH HÀNG (DOMAIN)
            var customer = _mapper.Map<Customer>(dto);
            customer.IdentityId = appUser.Id.ToString(); // Móc xích 2 bảng lại với nhau
            customer.RewardPoints = 0; // Khách mới mặc định 0 điểm

            await _unitOfWork.Repository<Customer>().AddAsync(customer);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<UserDto>(customer);
        }

        public async Task UpdateAsync(Guid id, UpdateUserDto dto)
        {
            // 1. Tìm hồ sơ khách hàng
            var customer = await _unitOfWork.Repository<Customer>().GetByIdAsync(id);
            if (customer == null) throw new Exception("Nỏ tìm thấy khách hàng ni.");

            // Kiểm tra SĐT mới có bị trùng không
            if (customer.PhoneNumber != dto.PhoneNumber)
            {
                var existing = await _unitOfWork.Repository<Customer>().GetAllPagedAsync(filter: u => u.PhoneNumber == dto.PhoneNumber);
                if (existing.TotalCount > 0) throw new Exception("Số điện thoại ni đã có người khác xài rồi.");
            }

            // 2. CẬP NHẬT BẢNG IDENTITY CHO ĐỒNG BỘ
            var appUser = await _userManager.FindByIdAsync(customer.IdentityId);
            if (appUser != null)
            {
                appUser.FullName = dto.FullName;
                appUser.PhoneNumber = dto.PhoneNumber;
                appUser.UserName = dto.PhoneNumber; // Đổi SĐT thì đổi luôn Username đăng nhập
                await _userManager.UpdateAsync(appUser);
            }

            // 3. CẬP NHẬT BẢNG CUSTOMER (DOMAIN)
            _mapper.Map(dto, customer);
            _unitOfWork.Repository<Customer>().Update(customer);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            // 1. Tìm khách hàng kèm theo danh sách đơn hàng
            var userWithOrders = await _unitOfWork.Repository<Customer>().GetAllPagedAsync(
                filter: u => u.Id == id,
                includeProperties: "Orders");

            var targetCustomer = userWithOrders.Items.FirstOrDefault();
            if (targetCustomer == null) throw new Exception("Nỏ tìm thấy khách hàng ni.");

            // Khách có đơn hàng rồi thì cấm xóa (để bảo toàn doanh thu)
            if (targetCustomer.Orders != null && targetCustomer.Orders.Any())
                throw new Exception("Khách ni đã mua hàng rồi, nỏ xóa được mô! Chỉ được khóa tài khoản thôi.");

            // 2. XÓA TÀI KHOẢN ĐĂNG NHẬP BÊN IDENTITY TRƯỚC
            var appUser = await _userManager.FindByIdAsync(targetCustomer.IdentityId);
            if (appUser != null)
            {
                await _userManager.DeleteAsync(appUser);
            }

            // 3. XÓA HỒ SƠ KHÁCH HÀNG BÊN DOMAIN
            _unitOfWork.Repository<Customer>().Delete(targetCustomer);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<List<CustomerCompactDto>> SearchCustomersForPosAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<CustomerCompactDto>();

            var search = keyword.Trim().ToLower();

            // Dùng GetQueryable để viết LINQ trực tiếp
            var customers = await _unitOfWork.Repository<Customer>().GetQueryable()
                .Where(c => c.FullName.ToLower().Contains(search) || c.PhoneNumber.Contains(search))
                .Take(5) // 👉 ĐIỂM ĂN TIỀN: Chỉ lấy tối đa 5 người đầu tiên tìm thấy (Cực nhanh)
                .Select(c => new CustomerCompactDto
                {
                    Id = c.Id,
                    FullName = c.FullName,
                    PhoneNumber = c.PhoneNumber,
                    // Points = c.Points 
                })
                .ToListAsync();

            return customers;
        }
    }
}