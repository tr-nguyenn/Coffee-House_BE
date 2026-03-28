using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Categories;

namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface ICategoryService
    {
        Task<PagedResult<CategoryDto>> GetAllPagedAsync(CategoryFilterDto filterDto);
        Task<CategoryDto?> GetByIdAsync(Guid id);
        Task<CategoryDto> CreateAsync(CreateUpdateCategoryDto dto);
        Task UpdateAsync(Guid id, CreateUpdateCategoryDto dto);
        Task DeleteAsync(Guid id);
    }
}
