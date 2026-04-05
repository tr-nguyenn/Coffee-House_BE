using CoffeeHouse.Application.DTOs.Reports.CoffeeHouse.Application.DTOs.Reports;
using CoffeeHouse.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeHouse.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardSummary([FromQuery] DashboardFilterDto filter)
        {
            try
            {
                var result = await _reportService.GetDashboardSummaryAsync(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Báo cáo Doanh thu theo ngày
        [HttpGet("revenue")]
        public async Task<IActionResult> GetRevenueReport([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            try
            {
                var result = await _reportService.GetRevenueReportAsync(startDate, endDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Báo cáo Top sản phẩm bán chạy
        [HttpGet("top-products")]
        public async Task<IActionResult> GetTopProductsReport([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            try
            {
                var result = await _reportService.GetTopProductsReportAsync(startDate, endDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Báo cáo Giờ cao điểm
        [HttpGet("peak-hours")]
        public async Task<IActionResult> GetPeakHoursReport([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            try
            {
                var result = await _reportService.GetPeakHoursReportAsync(startDate, endDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}