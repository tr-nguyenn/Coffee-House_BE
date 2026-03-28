using System.ComponentModel.DataAnnotations;

namespace CoffeeHouse.Application.DTOs.Orders
{
    public class CreateOrderDto
    {
        public Guid? TableId { get; set; } // Null nếu là mang về (Takeaway)
        public Guid? UserId { get; set; } // Null nếu là khách vãng lai không quét thẻ
        public string? Note { get; set; } // VD: "Khách mang về"
        public string PaymentMethod { get; set; } = "Cash";

        // Danh sách các món khách gọi
        public List<CreateOrderDetailDto> Items { get; set; } = new List<CreateOrderDetailDto>();

        // Tích điểm & Giảm giá (có thể để mặc định nếu chưa làm tới)
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
