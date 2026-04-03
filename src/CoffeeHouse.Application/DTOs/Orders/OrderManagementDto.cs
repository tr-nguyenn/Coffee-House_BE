namespace CoffeeHouse.Application.DTOs.Orders
{
    public class OrderManagementDto
    {
        public Guid Id { get; set; }
        public string OrderCode { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string? TableName { get; set; }
        public string? CustomerName { get; set; }
        public string? CashierName { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public string Status { get; set; } = null!;
        public string? PaymentMethod { get; set; }

        public List<OrderDetailManagementDto> OrderDetails { get; set; } = new();
    }

    public class OrderDetailManagementDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string? Note { get; set; }
    }
}
