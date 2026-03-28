using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Staffs;

namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface IStaffService
    {
        Task<PagedResult<StaffDto>> GetAllPagedAsync(StaffFilterDto filterDto);
        Task<StaffDto?> GetByIdAsync(Guid id);
        Task<StaffDto> CreateAsync(CreateStaffDto dto);
        Task UpdateAsync(Guid id, UpdateStaffDto dto);
        Task DeleteAsync(Guid id);

    }
}
