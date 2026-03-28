namespace CoffeeHouse.Application.Common
{
    public class BaseFilterDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Search { get; set; }
    }
}
