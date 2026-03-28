using CoffeeHouse.Domain.Enums;

namespace CoffeeHouse.Application.DTOs.Tables
{
    public class TableDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public TableStatus Status { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime? ActiveOrderTime { get; set; }

        public Guid AreaId { get; set; }
        public string AreaName { get; set; } = string.Empty;
    }
}
