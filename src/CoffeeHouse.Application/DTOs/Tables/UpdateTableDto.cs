using CoffeeHouse.Domain.Enums;

namespace CoffeeHouse.Application.DTOs.Tables
{
    public class UpdateTableDto
    {
        public string Name { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public TableStatus Status { get; set; } 
        public int DisplayOrder { get; set; }
        public Guid AreaId { get; set; }
    }
}
