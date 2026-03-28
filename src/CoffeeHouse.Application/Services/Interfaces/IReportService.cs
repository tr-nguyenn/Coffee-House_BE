using CoffeeHouse.Application.DTOs.Reports.CoffeeHouse.Application.DTOs.Reports;

namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface IReportService
    {
        Task<DashboardSummaryDto> GetDashboardSummaryAsync(DashboardFilterDto filter);
    }
}
