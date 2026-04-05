using CoffeeHouse.Application.DTOs.Reports.CoffeeHouse.Application.DTOs.Reports;

namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface IReportService
    {
        Task<DashboardSummaryDto> GetDashboardSummaryAsync(DashboardFilterDto filter);
        Task<List<RevenueReportItemDto>> GetRevenueReportAsync(DateTime startDate, DateTime endDate);
        Task<List<TopProductDto>> GetTopProductsReportAsync(DateTime startDate, DateTime endDate);
        Task<List<PeakHourDto>> GetPeakHoursReportAsync(DateTime startDate, DateTime endDate);
    }
}
