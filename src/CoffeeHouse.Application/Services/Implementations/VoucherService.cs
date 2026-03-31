using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Vouchers;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Domain.Entities;
using CoffeeHouse.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CoffeeHouse.Application.Services.Implementations
{
    public class VoucherService : IVoucherService
    {
        private readonly IUnitOfWork _unitOfWork;

        public VoucherService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // ========== CRUD ==========

        public async Task<PagedResult<VoucherDto>> GetAllPagedAsync(VoucherFilterDto filterDto)
        {
            var searchTerm = filterDto.Search?.Trim().ToLower();
            Expression<Func<Voucher, bool>>? filter = null;

            if (!string.IsNullOrWhiteSpace(searchTerm) && filterDto.ValidOnly == true)
            {
                var now = DateTime.UtcNow;
                filter = v => (v.Code.ToLower().Contains(searchTerm) || v.Description.ToLower().Contains(searchTerm)) &&
                              v.IsActive && v.UsedCount < v.UsageLimit && v.StartDate <= now && v.ExpiryDate >= now;
            }
            else if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                filter = v => v.Code.ToLower().Contains(searchTerm) || v.Description.ToLower().Contains(searchTerm);
            }
            else if (filterDto.ValidOnly == true)
            {
                var now = DateTime.UtcNow;
                filter = v => v.IsActive && v.UsedCount < v.UsageLimit && v.StartDate <= now && v.ExpiryDate >= now;
            }

            var result = await _unitOfWork.Repository<Voucher>().GetAllPagedAsync(
                pageNumber: filterDto.PageNumber,
                pageSize: filterDto.PageSize,
                filter: filter,
                orderBy: q => q.OrderByDescending(v => v.CreatedAt)
            );

            return new PagedResult<VoucherDto>
            {
                Items = result.Items.Select(MapToDto).ToList(),
                TotalCount = result.TotalCount,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize
            };
        }

        public async Task<VoucherDto?> GetByIdAsync(Guid id)
        {
            var voucher = await _unitOfWork.Repository<Voucher>().GetByIdAsync(id);
            return voucher == null ? null : MapToDto(voucher);
        }

        public async Task<VoucherDto> CreateAsync(CreateVoucherDto dto)
        {
            // Kiểm tra trùng mã
            var existing = await _unitOfWork.Repository<Voucher>()
                .GetFirstOrDefaultAsync(v => v.Code == dto.Code.Trim().ToUpper());
            if (existing != null)
                throw new Exception($"Mã voucher '{dto.Code}' đã tồn tại trong hệ thống!");

            var voucher = new Voucher
            {
                Code = dto.Code.Trim().ToUpper(),
                Description = dto.Description,
                DiscountType = dto.DiscountType,
                DiscountValue = dto.DiscountValue,
                MaxDiscountAmount = dto.MaxDiscountAmount,
                MinOrderAmount = dto.MinOrderAmount,
                StartDate = dto.StartDate,
                ExpiryDate = dto.ExpiryDate,
                UsageLimit = dto.UsageLimit,
                UsedCount = 0,
                IsActive = true
            };

            await _unitOfWork.Repository<Voucher>().AddAsync(voucher);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(voucher);
        }

        public async Task UpdateAsync(Guid id, UpdateVoucherDto dto)
        {
            var voucher = await _unitOfWork.Repository<Voucher>().GetByIdAsync(id);
            if (voucher == null) throw new Exception("Không tìm thấy voucher này.");

            // Kiểm tra trùng mã (ngoại trừ chính nó)
            var codeUpper = dto.Code.Trim().ToUpper();
            var duplicate = await _unitOfWork.Repository<Voucher>()
                .GetFirstOrDefaultAsync(v => v.Code == codeUpper && v.Id != id);
            if (duplicate != null)
                throw new Exception($"Mã voucher '{codeUpper}' đã được sử dụng bởi voucher khác!");

            voucher.Code = codeUpper;
            voucher.Description = dto.Description;
            voucher.DiscountType = dto.DiscountType;
            voucher.DiscountValue = dto.DiscountValue;
            voucher.MaxDiscountAmount = dto.MaxDiscountAmount;
            voucher.MinOrderAmount = dto.MinOrderAmount;
            voucher.StartDate = dto.StartDate;
            voucher.ExpiryDate = dto.ExpiryDate;
            voucher.UsageLimit = dto.UsageLimit;
            voucher.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<Voucher>().Update(voucher);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var voucher = await _unitOfWork.Repository<Voucher>().GetByIdAsync(id);
            if (voucher == null) throw new Exception("Không tìm thấy voucher này.");

            // Kiểm tra xem voucher đã được sử dụng chưa
            if (voucher.UsedCount > 0)
                throw new Exception("Không thể xóa voucher đã có lượt sử dụng. Hãy tắt trạng thái thay vì xóa.");

            _unitOfWork.Repository<Voucher>().Delete(voucher);
            await _unitOfWork.SaveChangesAsync();
        }

        // ========== TOGGLE & VALIDATE ==========

        public async Task ToggleActiveAsync(Guid id)
        {
            var voucher = await _unitOfWork.Repository<Voucher>().GetByIdAsync(id);
            if (voucher == null) throw new Exception("Không tìm thấy voucher này.");

            voucher.IsActive = !voucher.IsActive;
            voucher.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<Voucher>().Update(voucher);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<VoucherDto> ValidateVoucherAsync(string code, decimal orderTotalAmount)
        {
            var voucher = await _unitOfWork.Repository<Voucher>()
                .GetFirstOrDefaultAsync(v => v.Code == code.Trim().ToUpper());

            if (voucher == null)
                throw new Exception("Mã giảm giá không tồn tại!");

            if (!voucher.IsActive)
                throw new Exception("Mã giảm giá đã bị vô hiệu hóa!");

            var now = DateTime.UtcNow;
            if (now < voucher.StartDate)
                throw new Exception($"Mã giảm giá chưa đến thời gian áp dụng (từ {voucher.StartDate:dd/MM/yyyy}).");

            if (now > voucher.ExpiryDate)
                throw new Exception("Mã giảm giá đã hết hạn sử dụng!");

            if (voucher.UsedCount >= voucher.UsageLimit)
                throw new Exception("Mã giảm giá đã hết lượt sử dụng!");

            if (orderTotalAmount < voucher.MinOrderAmount)
                throw new Exception($"Đơn hàng phải từ {voucher.MinOrderAmount:N0}đ trở lên mới được áp dụng mã này.");

            return MapToDto(voucher);
        }

        // ========== MAPPER THỦ CÔNG ==========

        private static VoucherDto MapToDto(Voucher v) => new()
        {
            Id = v.Id,
            Code = v.Code,
            Description = v.Description,
            DiscountType = v.DiscountType.ToString(),
            DiscountValue = v.DiscountValue,
            MaxDiscountAmount = v.MaxDiscountAmount,
            MinOrderAmount = v.MinOrderAmount,
            StartDate = v.StartDate,
            ExpiryDate = v.ExpiryDate,
            UsageLimit = v.UsageLimit,
            UsedCount = v.UsedCount,
            IsActive = v.IsActive,
            CreatedAt = v.CreatedAt
        };
    }
}
