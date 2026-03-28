using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Area;
using System.Threading.Tasks;

namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface IAreaService
    {
        Task<PagedResult<AreaDto>> GetAllPagedAsync(AreaFilterDto filterDto);
        Task<AreaDto?> GetByIdAsync(Guid id);
        Task<AreaDto> CreateAsync(CreateAreaDto dto);
        Task UpdateAsync(Guid id, UpdateAreaDto dto);
        Task DeleteAsync(Guid id);
    }
}
