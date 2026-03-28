using AutoMapper;
using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Area;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Domain.Entities;
using System.Linq.Expressions;

namespace CoffeeHouse.Application.Services.Implementations
{
    public class AreaService : IAreaService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public AreaService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<PagedResult<AreaDto>> GetAllPagedAsync(AreaFilterDto filterDto)
        {
            var searchTerm = filterDto.Search?.Trim().ToLower();
            Expression<Func<Area, bool>>? filter = null;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                filter = a => a.Name.ToLower().Contains(searchTerm);
            }

            var result = await _unitOfWork.Repository<Area>().GetAllPagedAsync(
                pageNumber: filterDto.PageNumber, // Thuộc tính này lấy từ BaseFilterDto
                pageSize: filterDto.PageSize,     // Thuộc tính này lấy từ BaseFilterDto
                filter: filter,
                orderBy: q => q.OrderBy(a => a.DisplayOrder) // Ưu tiên sắp xếp theo thứ tự hiển thị
            );

            return new PagedResult<AreaDto>
            {
                Items = _mapper.Map<List<AreaDto>>(result.Items),
                TotalCount = result.TotalCount,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize
            };
        }

        public async Task<AreaDto?> GetByIdAsync(Guid id)
        {
            var area = await _unitOfWork.Repository<Area>().GetByIdAsync(id);
            return _mapper.Map<AreaDto>(area);
        }

        public async Task<AreaDto> CreateAsync(CreateAreaDto dto)
        {
            // Kiểm tra trùng tên khu vực
            var existing = await _unitOfWork.Repository<Area>().GetAllPagedAsync(filter: a => a.Name == dto.Name);
            if (existing.TotalCount > 0) throw new Exception("Tên khu vực này đã tồn tại.");

            var area = _mapper.Map<Area>(dto);
            await _unitOfWork.Repository<Area>().AddAsync(area);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<AreaDto>(area);
        }

        public async Task UpdateAsync(Guid id, UpdateAreaDto dto)
        {
            var area = await _unitOfWork.Repository<Area>().GetByIdAsync(id);
            if (area == null) throw new Exception("Không tìm thấy khu vực.");

            // Ánh xạ dữ liệu mới vào thực thể cũ
            _mapper.Map(dto, area);
            // area.UpdatedAt = DateTime.UtcNow; // Mở comment dòng này nếu BaseEntity của bạn có UpdatedAt

            _unitOfWork.Repository<Area>().Update(area);
            await _unitOfWork.SaveChangesAsync();
        }
        public async Task DeleteAsync(Guid id)
        {
            // Ràng buộc toàn vẹn: Không cho xóa nếu khu vực đang chứa Bàn
            var areaWithTables = await _unitOfWork.Repository<Area>().GetAllPagedAsync(
                filter: a => a.Id == id,
                includeProperties: "Tables");

            var target = areaWithTables.Items.FirstOrDefault();
            if (target == null) throw new Exception("Không tìm thấy khu vực.");

            if (target.Tables != null && target.Tables.Any())
                throw new Exception("Không thể xóa vì đang có Bàn thuộc khu vực này.");

            _unitOfWork.Repository<Area>().Delete(target);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
