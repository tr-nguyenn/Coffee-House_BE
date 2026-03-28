using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Users;

namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface IUserService
    {
        Task<PagedResult<UserDto>> GetAllPagedAsync(UserFilterDto filterDto);
        Task<UserDto?> GetByIdAsync(Guid id);
        Task<UserDto> CreateAsync(CreateUserDto dto);
        Task UpdateAsync(Guid id, UpdateUserDto dto);
        Task DeleteAsync(Guid id);
        Task<List<CustomerCompactDto>> SearchCustomersForPosAsync(string keyword);
    }
}
