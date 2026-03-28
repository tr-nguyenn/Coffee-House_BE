namespace CoffeeHouse.Application.DTOs.Orders
{
    public class OrderDto
    {
        public Guid Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public decimal FinalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderDetailDto> OrderDetails { get; set; } = new List<OrderDetailDto>();
    }

    public class OrderDetailDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string? Note { get; set; }
    }

    // DTO để gọi thêm món vào Bill cũ
    public class AddOrderItemsDto
    {
        public List<CreateOrderDetailDto> NewItems { get; set; } = new List<CreateOrderDetailDto>();
    }

    // DTO để đổ dữ liệu ra Cột Trái (Danh sách Bàn kèm trạng thái)
    public class TableStatusDto
    {
        public Guid TableId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public string AreaName { get; set; } = string.Empty;
        public bool IsInUse { get; set; }
        public Guid? ActiveOrderId { get; set; }
        public string? ActiveOrderCode { get; set; }
        public DateTime? ActiveOrderTime { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class CheckoutOrderDto
    {
        public string PaymentMethod { get; set; } = "Cash"; // Tiền mặt (Cash) hoặc Chuyển khoản (Transfer)
        // Sau này mi có thể thêm Khách đưa bao nhiêu, thối lại bao nhiêu vô đây
        public Guid? CustomerId { get; set; } // Nếu chọn từ danh sách khách có sẵn
        public string? CustomerName { get; set; } // Nếu là khách vãng lai đọc tên
        public string? CustomerPhone { get; set; } // Nếu khách vãng lai đọc SĐT

        public string? VoucherCode { get; set; } // Mã giảm giá khách đưa

        // (Tùy chọn) Tính tiền thối
        public decimal AmountTendered { get; set; } // Tiền khách đưa
    }
}
