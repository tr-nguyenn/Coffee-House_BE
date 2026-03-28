using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Products;

namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface IProductService
    {
        Task<PagedResult<ProductDto>> GetAllPagedAsync(ProductFilterDto filterDto);
        Task<ProductDto?> GetByIdAsync(Guid id);
        Task<ProductDto> CreateAsync(CreateUpdateProductDto dto);
        Task UpdateAsync(Guid id, CreateUpdateProductDto dto);
        Task DeleteAsync(Guid id);
    }
}
