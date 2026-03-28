using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Orders;
using CoffeeHouse.Application.DTOs.Tables;

namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface ITableService
    {
        Task<PagedResult<TableDto>> GetAllPagedAsync(TableFilterDto filterDto);
        Task<TableDto?> GetByIdAsync(Guid id);
        Task<TableDto> CreateAsync(CreateTableDto dto);
        Task UpdateAsync(Guid id, UpdateTableDto dto);
        Task DeleteAsync(Guid id);
        Task<List<TableStatusDto>> GetTablesWithStatusAsync();
    }
}
