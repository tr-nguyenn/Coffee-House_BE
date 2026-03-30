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
            var result = await _unitOfWork.Repository<Table>()
                .GetQueryable()
                .AsNoTracking()
                .Select(t => new TableStatusDto
                {
                    TableId = t.Id,
                    TableName = t.Name,
                    AreaName = t.Area != null ? t.Area.Name : "Chưa xếp",
                    DisplayOrder = t.DisplayOrder,
                    AreaDisplayOrder = t.Area != null ? t.Area.DisplayOrder : 9999, // Phép màu nằm ở đây
                    IsInUse = t.Orders.Any(o => o.Status == OrderStatus.Processing),
                    ActiveOrderId = t.Orders.Where(o => o.Status == OrderStatus.Processing)
                                            .Select(o => (Guid?)o.Id).FirstOrDefault(),
                    ActiveOrderCode = t.Orders.Where(o => o.Status == OrderStatus.Processing)
                                              .Select(o => o.OrderCode).FirstOrDefault(),
                    ActiveOrderTime = t.Orders.Where(o => o.Status == OrderStatus.Processing)
                                              .Select(o => (DateTime?)o.CreatedAt).FirstOrDefault()
                })
                .OrderBy(dto => dto.AreaDisplayOrder)
                .ThenBy(dto => dto.DisplayOrder)
                .ToListAsync();

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
