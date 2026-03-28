using AutoMapper;
using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Orders;
using CoffeeHouse.Application.DTOs.Tables;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Domain.Entities;
using CoffeeHouse.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CoffeeHouse.Application.Services.Implementations
{
    public class TableService : ITableService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public TableService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<List<TableStatusDto>> GetTablesWithStatusAsync()
        {
            // 1. Lấy TẤT CẢ các bàn, chỉ include Khu vực (Area) thôi, KHÔNG include Orders kiểu phức tạp nữa
            var tables = await _unitOfWork.Repository<Table>()
                .GetQueryable()
                .Include(t => t.Area)
                .ToListAsync();

            // 2. Lấy TẤT CẢ các đơn hàng đang ở trạng thái Processing (đang phục vụ) trong DB ra
            var activeOrders = await _unitOfWork.Repository<Order>()
                .GetQueryable()
                .Where(o => o.Status == OrderStatus.Processing)
                .ToListAsync();

            // 3. Gom nhóm: Duyệt qua danh sách bàn và so sánh bằng code C#
            var result = tables.Select(t => {
                var activeOrder = activeOrders.FirstOrDefault(o => o.TableId == t.Id);

                return new TableStatusDto
                {
                    TableId = t.Id,
                    TableName = t.Name,
                    AreaName = t.Area != null ? t.Area.Name : "N/A",
                    IsInUse = activeOrder != null, // true: Đỏ (Có khách), false: Xanh (Trống)
                    ActiveOrderId = activeOrder?.Id,
                    ActiveOrderCode = activeOrder?.OrderCode
                };
            }).ToList();

            return result;
        }

        public async Task<PagedResult<TableDto>> GetAllPagedAsync(TableFilterDto filterDto)
        {
            var searchTerm = filterDto.Search?.Trim().ToLower();

            // Xây dựng bộ lọc động
            Expression<Func<Table, bool>> filter = t =>
                (string.IsNullOrWhiteSpace(searchTerm) || t.Name.ToLower().Contains(searchTerm)) &&
                (!filterDto.AreaId.HasValue || t.AreaId == filterDto.AreaId.Value) &&
                (!filterDto.Status.HasValue || t.Status == filterDto.Status.Value);

            // 1. Lấy danh sách Bàn (có phân trang)
            var result = await _unitOfWork.Repository<Table>().GetAllPagedAsync(
                pageNumber: filterDto.PageNumber,
                pageSize: filterDto.PageSize,
                filter: filter,
                orderBy: q => q.OrderBy(t => t.Area.DisplayOrder).ThenBy(t => t.DisplayOrder),
                includeProperties: "Area"
            );

            // 2. Map sang DTO trước
            var tableDtos = _mapper.Map<List<TableDto>>(result.Items);

            // 3. LẤY THỜI GIAN ORDER HIỆN TẠI (Tối ưu hiệu năng)
            if (tableDtos.Any())
            {
                var tableIds = tableDtos.Select(t => t.Id).ToList();

                // Chỉ móc lên những Order của các bàn trong trang này VÀ đang ở trạng thái Processing
                var activeOrders = await _unitOfWork.Repository<Order>()
                    .GetQueryable()
                    .Where(o => tableIds.Contains(o.TableId.Value) && o.Status == OrderStatus.Processing) // Nhớ check TableId có .Value không nhé
                    .ToListAsync();

                // Ghép thời gian vào từng Bàn
                foreach (var dto in tableDtos)
                {
                    // Lấy Order mới nhất (đề phòng lỗi rác có 2 order cùng lúc)
                    var activeOrder = activeOrders
                        .Where(o => o.TableId == dto.Id)
                        .OrderByDescending(o => o.CreatedAt)
                        .FirstOrDefault();

                    if (activeOrder != null)
                    {
                        dto.ActiveOrderTime = activeOrder.CreatedAt;
                    }
                }
            }

            return new PagedResult<TableDto>
            {
                Items = tableDtos,
                TotalCount = result.TotalCount,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize
            };
        }

        public async Task<TableDto?> GetByIdAsync(Guid id)
        {
            var tables = await _unitOfWork.Repository<Table>().GetAllPagedAsync(
                filter: t => t.Id == id,
                includeProperties: "Area");

            return _mapper.Map<TableDto>(tables.Items.FirstOrDefault());
        }

        public async Task<TableDto> CreateAsync(CreateTableDto dto)
        {
            // Kiểm tra AreaId có tồn tại không
            var areaExists = await _unitOfWork.Repository<Area>().GetByIdAsync(dto.AreaId);
            if (areaExists == null) throw new Exception("Khu vực không tồn tại.");

            // Kiểm tra trùng tên bàn trong CÙNG MỘT khu vực
            var existing = await _unitOfWork.Repository<Table>().GetAllPagedAsync(
                filter: t => t.Name == dto.Name && t.AreaId == dto.AreaId);
            if (existing.TotalCount > 0) throw new Exception("Tên bàn này đã tồn tại trong khu vực được chọn.");

            var table = _mapper.Map<Table>(dto);
            table.Status = CoffeeHouse.Domain.Enums.TableStatus.Available;

            await _unitOfWork.Repository<Table>().AddAsync(table);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<TableDto>(table);
        }

        public async Task UpdateAsync(Guid id, UpdateTableDto dto)
        {
            var table = await _unitOfWork.Repository<Table>().GetByIdAsync(id);
            if (table == null) throw new Exception("Không tìm thấy bàn.");

            // Nếu đổi sang khu vực khác, phải kiểm tra khu vực mới có tồn tại không
            if (table.AreaId != dto.AreaId)
            {
                var areaExists = await _unitOfWork.Repository<Area>().GetByIdAsync(dto.AreaId);
                if (areaExists == null) throw new Exception("Khu vực mới không tồn tại.");
            }

            _mapper.Map(dto, table);
            _unitOfWork.Repository<Table>().Update(table);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var table = await _unitOfWork.Repository<Table>().GetByIdAsync(id);
            if (table == null) throw new Exception("Không tìm thấy bàn.");

            // TODO: Sau này làm module Order, bạn sẽ cần thêm logic:
            // "Không được xóa bàn nếu bàn đang có Order trạng thái Chưa thanh toán"

            if (table.Status == CoffeeHouse.Domain.Enums.TableStatus.Occupied)
                throw new Exception("Không thể xóa bàn đang có khách ngồi.");

            _unitOfWork.Repository<Table>().Delete(table);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
