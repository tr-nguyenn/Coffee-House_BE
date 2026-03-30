namespace CoffeeHouse.Application.DTOs.Orders
{
    public class OrderFilterDto
    {
        public string? Search { get; set; }
        public string? TimeRange { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<string>? Statuses { get; set; }
        public string? PaymentMethod { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
