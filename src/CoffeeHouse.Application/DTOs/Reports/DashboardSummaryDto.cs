namespace CoffeeHouse.Application.DTOs.Reports
{
    namespace CoffeeHouse.Application.DTOs.Reports
    {
        // Filter để chủ quán chọn từ ngày - đến ngày
        public class DashboardFilterDto
        {
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }
        }

        // Cục JSON tổng bọc tất cả dữ liệu
        public class DashboardSummaryDto
        {
            public decimal TotalRevenue { get; set; }
            public int TotalOrders { get; set; }
            public decimal AverageOrderValue { get; set; }
            public int TotalCustomers { get; set; }

            public List<RevenueByDateDto> RevenueTrends { get; set; } = new();
            public List<TopProductDto> TopSellingProducts { get; set; } = new();
            public List<PaymentMethodStatDto> PaymentMethodStats { get; set; } = new();
        }

        // Biểu đồ đường (Line Chart): Doanh thu theo ngày
        public class RevenueByDateDto
        {
            public string Date { get; set; } = string.Empty;
            public decimal Revenue { get; set; }
        }

        // Biểu đồ tròn/Danh sách: Món bán chạy nhất
        public class TopProductDto
        {
            public string ProductName { get; set; } = string.Empty;
            public int TotalQuantity { get; set; }
            public decimal TotalRevenue { get; set; }
        }

        // Biểu đồ tròn: Tỷ lệ tiền mặt vs Chuyển khoản
        public class PaymentMethodStatDto
        {
            public string PaymentMethod { get; set; } = string.Empty;
            public int Count { get; set; }
            public decimal TotalAmount { get; set; }
        }
    }
}
