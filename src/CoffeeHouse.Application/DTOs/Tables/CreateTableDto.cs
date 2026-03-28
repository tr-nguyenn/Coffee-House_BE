namespace CoffeeHouse.Application.DTOs.Tables
{
    public class CreateTableDto
    {
        public string Name { get; set; } = string.Empty;
        public int Capacity { get; set; } = 4;
        public int DisplayOrder { get; set; }
        public Guid AreaId { get; set; }
    }
}
