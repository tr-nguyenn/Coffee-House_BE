using AutoMapper;
using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Staffs;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Infrastructure;
using Microsoft.AspNetCore.Identity;

namespace CoffeeHouse.Application.Services.Implementations
{
    public class StaffService : IStaffService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly IMapper _mapper;

        public StaffService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            IMapper mapper)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _mapper = mapper;
        }

        public async Task<PagedResult<StaffDto>> GetAllPagedAsync(StaffFilterDto filterDto)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(filterDto.Search))
            {
                var search = filterDto.Search!.Trim().ToLower();
                query = query.Where(u => u.Email.ToLower().Contains(search) ||
                                         u.FullName.ToLower().Contains(search));
            }

            // 👉 1. TÌM TẤT CẢ ID CỦA NHỮNG NGƯỜI LÀ "CUSTOMER"
            var customers = await _userManager.GetUsersInRoleAsync("Customer");
            var customerIds = customers.Select(c => c.Id).ToList();

            // 👉 2. LOẠI TRỪ HỌ KHỎI CÂU TRUY VẤN
            query = query.Where(u => !customerIds.Contains(u.Id));

            var totalCount = query.Count();

            var users = query
                .Skip((filterDto.PageNumber - 1) * filterDto.PageSize)
                .Take(filterDto.PageSize)
                .ToList();

            var staffDtos = new List<StaffDto>();
            foreach (var user in users)
            {
                var dto = _mapper.Map<StaffDto>(user);
                dto.Roles = await _userManager.GetRolesAsync(user);
                staffDtos.Add(dto);
            }

            // Lọc theo Role cụ thể (nếu Admin muốn lọc trên UI)
            if (!string.IsNullOrWhiteSpace(filterDto.Role))
            {
                staffDtos = staffDtos.Where(s => s.Roles.Contains(filterDto.Role)).ToList();
                totalCount = staffDtos.Count;
            }

            return new PagedResult<StaffDto>
            {
                Items = staffDtos,
                TotalCount = totalCount,
                PageNumber = filterDto.PageNumber,
                PageSize = filterDto.PageSize
            };
        }

        public async Task<StaffDto?> GetByIdAsync(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return null;

            var dto = _mapper.Map<StaffDto>(user);
            dto.Roles = await _userManager.GetRolesAsync(user);
            return dto;
        }

        public async Task<StaffDto> CreateAsync(CreateStaffDto dto)
        {
            var existing = await _userManager.FindByEmailAsync(dto.Email);
            if (existing != null) throw new Exception("Email ni đã có người xài rồi.");

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded) throw new Exception(result.Errors.First().Description);

            // Gán quyền
            if (await _roleManager.RoleExistsAsync(dto.Role))
            {
                await _userManager.AddToRoleAsync(user, dto.Role);
            }

            var returnDto = _mapper.Map<StaffDto>(user);
            returnDto.Roles = new List<string> { dto.Role };
            return returnDto;
        }

        public async Task UpdateAsync(Guid id, UpdateStaffDto dto)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) throw new Exception("Nỏ tìm thấy nhân viên.");

            // Cập nhật thông tin cơ bản
            user.FullName = dto.FullName;
            user.PhoneNumber = dto.PhoneNumber;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded) throw new Exception(result.Errors.First().Description);

            // Cập nhật Quyền (Xóa quyền cũ, thêm quyền mới)
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            if (await _roleManager.RoleExistsAsync(dto.Role))
            {
                await _userManager.AddToRoleAsync(user, dto.Role);
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) throw new Exception("Nỏ tìm thấy nhân viên.");

            // Nên chặn việc tự xóa chính mình nếu cần
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded) throw new Exception("Xóa nhân viên thất bại.");
        }
    }
}