namespace CoffeeHouse.Application.DTOs.Orders
{
    public class KitchenTicketDto
    {
        public Guid OrderDetailId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string? Note { get; set; }
        public string TableName { get; set; } = string.Empty;
        public DateTime? PrepStartTime { get; set; }
    }
}
