using AutoMapper;
using CoffeeHouse.Application.DTOs.Invoices;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Domain.Entities;
using CoffeeHouse.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoffeeHouse.Application.Services.Implementations
{
    public class InvoiceService : IInvoiceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public InvoiceService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<InvoicePagedResult> GetInvoicesPagedAsync(InvoiceFilterDto filter)
        {
            var query = _unitOfWork.Repository<Order>().GetQueryable()
                .Include(o => o.Table)
                // 👉 CHỈ LẤY CÁC ĐƠN ĐÃ THANH TOÁN THÀNH CÔNG
                .Where(o => o.Status == OrderStatus.Completed);

            // 1. Áp dụng Bộ Lọc Ngày Tháng
            if (filter.FromDate.HasValue)
                query = query.Where(o => o.CreatedAt >= filter.FromDate.Value);

            if (filter.ToDate.HasValue)
            {
                // Cộng thêm 1 ngày và trừ đi 1 tick để lấy trọn vẹn đến 23:59:59 của ngày ToDate
                var toDateEnd = filter.ToDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(o => o.CreatedAt <= toDateEnd);
            }

            // Lọc theo từ khóa (Tìm mã Hóa đơn)
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim().ToLower();
                query = query.Where(o => o.Id.ToString().ToLower().Contains(search));
                // Nếu mi có cột InvoiceCode thì đổi thành: o.InvoiceCode.ToLower().Contains(search)
            }

            // 2. TÍNH TỔNG DOANH THU CỦA CÁC ĐƠN ĐANG LỌC (Trước khi phân trang)
            var totalRevenue = await query.SumAsync(o => o.FinalAmount);

            // 3. Phân trang
            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(o => o.UpdatedAt) // Xếp hóa đơn mới nhất lên đầu
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            // Map Entity -> DTO thủ công cho lẹ, khỏi đụng AutoMapper khúc này
            var dtoList = items.Select(o => new InvoiceDto
            {
                Id = o.Id,
                InvoiceCode = o.Id.ToString().Substring(0, 8).ToUpper(), // Tạm cắt ID ra làm Mã HĐ cho đẹp
                TableName = o.Table != null ? o.Table.Name : "Mang đi",
                CheckInTime = o.CreatedAt,
                CheckOutTime = o.UpdatedAt,
                FinalAmount = o.FinalAmount,
                Status = "Đã thanh toán"
            }).ToList();

            return new InvoicePagedResult
            {
                Items = dtoList,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalRevenue = totalRevenue // 👉 Điểm ăn tiền ở góc dưới màn hình là đây!
            };
        }

        public async Task<InvoiceDetailDto> GetInvoiceDetailAsync(Guid orderId)
        {
            var order = await _unitOfWork.Repository<Order>().GetQueryable()
                .Include(o => o.Table)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.Status == OrderStatus.Completed);

            if (order == null) throw new Exception("Không tìm thấy hóa đơn này.");

            return new InvoiceDetailDto
            {
                Id = order.Id,
                InvoiceCode = order.Id.ToString().Substring(0, 8).ToUpper(),
                TableName = order.Table != null ? order.Table.Name : "Mang đi",
                CheckInTime = order.CreatedAt,
                CheckOutTime = order.UpdatedAt,
                FinalAmount = order.FinalAmount,
                SubTotal = order.TotalAmount,
                Discount = order.DiscountAmount,
                Items = order.OrderDetails.Select(od => new InvoiceItemDto
                {
                    ProductName = od.Product?.Name ?? "Sản phẩm ẩn",
                    Quantity = od.Quantity,
                    UnitPrice = od.UnitPrice
                }).ToList()
            };
        }
    }
}
