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
    }
}
