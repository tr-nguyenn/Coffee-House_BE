using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Vouchers;

namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface IVoucherService
    {
        Task<PagedResult<VoucherDto>> GetAllPagedAsync(VoucherFilterDto filterDto);
        Task<VoucherDto?> GetByIdAsync(Guid id);
        Task<VoucherDto> CreateAsync(CreateVoucherDto dto);
        Task UpdateAsync(Guid id, UpdateVoucherDto dto);
        Task DeleteAsync(Guid id);
        Task ToggleActiveAsync(Guid id);
        Task<VoucherDto> ValidateVoucherAsync(string code, decimal orderTotalAmount);
    }
}
