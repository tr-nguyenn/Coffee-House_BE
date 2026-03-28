namespace CoffeeHouse.Application.DTOs.Orders
{
    public  class CreateOrderDetailDto
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public string? Note { get; set; } 
    }
}
