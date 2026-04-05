using CoffeeHouse.Application.DTOs.Reports.CoffeeHouse.Application.DTOs.Reports;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Domain.Entities;
using CoffeeHouse.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoffeeHouse.Application.Services.Implementations
{
    public class ReportService : IReportService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ReportService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(DashboardFilterDto filter)
        {
            // 1. Lấy dữ liệu thô: Chỉ lấy những đơn hàng ĐÃ HOÀN THÀNH (Completed)
            var query = _unitOfWork.Repository<Order>().GetQueryable()
                .Where(o => o.Status == OrderStatus.Completed);

            // Áp dụng bộ lọc thời gian (nếu có)
            if (filter.StartDate.HasValue)
                query = query.Where(o => o.CreatedAt >= filter.StartDate.Value);

            if (filter.EndDate.HasValue)
                query = query.Where(o => o.CreatedAt <= filter.EndDate.Value.AddDays(1).AddTicks(-1)); // Lấy trọn ngày kết thúc

            var completedOrders = await query.ToListAsync();

            // Kéo OrderDetails của các đơn thành công để tính món bán chạy
            var orderIds = completedOrders.Select(o => o.Id).ToList();
            var orderDetails = await _unitOfWork.Repository<OrderDetail>().GetQueryable()
                .Include(od => od.Product)
                .Where(od => orderIds.Contains(od.OrderId))
                .ToListAsync();

            // 2. Tính toán các chỉ số (Nhồi vào DTO)
            var summary = new DashboardSummaryDto();

            // KPIs Tổng quan
            summary.TotalRevenue = completedOrders.Sum(o => o.FinalAmount);
            summary.TotalOrders = completedOrders.Count;
            summary.AverageOrderValue = summary.TotalOrders > 0 ? summary.TotalRevenue / summary.TotalOrders : 0;
            summary.TotalCustomers = completedOrders.Where(o => o.CustomerId != null).Select(o => o.CustomerId).Distinct().Count();

            // Trend: Doanh thu theo từng ngày (Nhóm theo ngày)
            summary.RevenueTrends = completedOrders
                .GroupBy(o => o.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new RevenueByDateDto
                {
                    Date = g.Key.ToString("dd/MM/yyyy"),
                    Revenue = g.Sum(o => o.FinalAmount)
                }).ToList();

            // Top món bán chạy (Nhóm theo tên sản phẩm)
            summary.TopSellingProducts = orderDetails
                .GroupBy(od => od.Product?.Name ?? "Sản phẩm bị xóa")
                .Select(g => new TopProductDto
                {
                    ProductName = g.Key,
                    TotalQuantity = g.Sum(od => od.Quantity),
                    TotalRevenue = g.Sum(od => od.Quantity * od.UnitPrice)
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(5) // Lấy top 5 món
                .ToList();

            // Tỷ lệ phương thức thanh toán
            summary.PaymentMethodStats = completedOrders
                .GroupBy(o => o.PaymentMethod)
                .Select(g => new PaymentMethodStatDto
                {
                    PaymentMethod = string.IsNullOrEmpty(g.Key) ? "Cash" : g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(o => o.FinalAmount)
                }).ToList();

            return summary;
        }

        // =============================================
        // BÁO CÁO 1: DOANH THU THEO NGÀY
        // Nhóm theo ngày -> Tổng doanh thu + Số đơn
        // =============================================
        public async Task<List<RevenueReportItemDto>> GetRevenueReportAsync(DateTime startDate, DateTime endDate)
        {
            var endOfDay = endDate.Date.AddDays(1).AddTicks(-1);

            var orders = await _unitOfWork.Repository<Order>().GetQueryable()
                .Where(o => o.Status == OrderStatus.Completed)
                .Where(o => o.CreatedAt >= startDate.Date && o.CreatedAt <= endOfDay)
                .ToListAsync();

            // Nhóm theo Date, tính tổng doanh thu và đếm số đơn mỗi ngày
            var result = orders
                .GroupBy(o => o.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new RevenueReportItemDto
                {
                    Date = g.Key.ToString("dd/MM/yyyy"),
                    TotalRevenue = g.Sum(o => o.FinalAmount),
                    OrderCount = g.Count()
                })
                .ToList();

            return result;
        }

        // =============================================
        // BÁO CÁO 2: TOP 10 SẢN PHẨM BÁN CHẠY
        // Join Order -> OrderDetail -> Product
        // =============================================
        public async Task<List<TopProductDto>> GetTopProductsReportAsync(DateTime startDate, DateTime endDate)
        {
            var endOfDay = endDate.Date.AddDays(1).AddTicks(-1);

            // Lấy danh sách ID các đơn hoàn thành trong khoảng thời gian
            var completedOrderIds = await _unitOfWork.Repository<Order>().GetQueryable()
                .Where(o => o.Status == OrderStatus.Completed)
                .Where(o => o.CreatedAt >= startDate.Date && o.CreatedAt <= endOfDay)
                .Select(o => o.Id)
                .ToListAsync();

            // Join bảng OrderDetail với Product, lọc theo đơn đã hoàn thành
            var orderDetails = await _unitOfWork.Repository<OrderDetail>().GetQueryable()
                .Include(od => od.Product)
                .Where(od => completedOrderIds.Contains(od.OrderId))
                .ToListAsync();

            // Nhóm theo tên sản phẩm, tính tổng số lượng và doanh thu
            var result = orderDetails
                .GroupBy(od => od.Product?.Name ?? "Sản phẩm bị xóa")
                .Select(g => new TopProductDto
                {
                    ProductName = g.Key,
                    TotalQuantity = g.Sum(od => od.Quantity),
                    TotalRevenue = g.Sum(od => od.Quantity * od.UnitPrice)
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(10) // Top 10
                .ToList();

            return result;
        }

        // =============================================
        // BÁO CÁO 3: GIỜ CAO ĐIỂM (0h - 23h)
        // Nhóm theo giờ trong ngày -> Đếm đơn
        // =============================================
        public async Task<List<PeakHourDto>> GetPeakHoursReportAsync(DateTime startDate, DateTime endDate)
        {
            var endOfDay = endDate.Date.AddDays(1).AddTicks(-1);

            var orders = await _unitOfWork.Repository<Order>().GetQueryable()
                .Where(o => o.Status == OrderStatus.Completed)
                .Where(o => o.CreatedAt >= startDate.Date && o.CreatedAt <= endOfDay)
                .ToListAsync();

            // Nhóm theo giờ (Hour) của CreatedAt
            var grouped = orders
                .GroupBy(o => o.CreatedAt.Hour)
                .ToDictionary(g => g.Key, g => g.Count());

            // Tạo đủ 24 giờ (0h-23h) để biểu đồ hiển thị đầy đủ
            var result = Enumerable.Range(0, 24)
                .Select(hour => new PeakHourDto
                {
                    Hour = hour,
                    OrderCount = grouped.GetValueOrDefault(hour, 0)
                })
                .ToList();

            return result;
        }
    }
}
