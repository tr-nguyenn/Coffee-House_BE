using System.ComponentModel.DataAnnotations;

namespace CoffeeHouse.Application.DTOs.Orders
{
    public class CreateOrderDto
    {
        public Guid? TableId { get; set; }
        public Guid? CustomerId { get; set; } 
        public string? Note { get; set; } 
        public string PaymentMethod { get; set; } = "Cash";

        public List<CreateOrderDetailDto> Items { get; set; } = new List<CreateOrderDetailDto>();

        public Guid? VoucherId { get; set; }
        public int PointsUsed { get; set; } = 0;
    }

    public class OrderItemDto
    {
        [Required]
        public Guid ProductId { get; set; }

        [Range(1, 100, ErrorMessage = "Số lượng phải từ 1 đến 100")]
        public int Quantity { get; set; }
    }
}
