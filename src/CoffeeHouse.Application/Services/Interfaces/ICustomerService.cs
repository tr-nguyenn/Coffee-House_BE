using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Customer;

namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface ICustomerService
    {
        Task<PagedResult<UserDto>> GetAllPagedAsync(CustomerFilterDto filterDto);
        Task<UserDto?> GetByIdAsync(Guid id);
        Task<UserDto> CreateAsync(CreateUserDto dto);
        Task UpdateAsync(Guid id, UpdateUserDto dto);
        Task DeleteAsync(Guid id);
        Task<List<CustomerCompactDto>> SearchCustomersForPosAsync(string keyword);
    }
}
