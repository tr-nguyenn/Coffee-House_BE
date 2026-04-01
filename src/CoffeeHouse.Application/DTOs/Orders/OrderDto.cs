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

    public class AddOrderItemsDto
    {
        public List<CreateOrderDetailDto> NewItems { get; set; } = new List<CreateOrderDetailDto>();
    }

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
        public int AreaDisplayOrder { get; set; }
        public string? PaymentMethod { get; set; }
    }

    public class CheckoutOrderDto
    {
        public string PaymentMethod { get; set; } = "Cash"; 
        public Guid? CustomerId { get; set; } 
        public string? CustomerName { get; set; } 
        public string? CustomerPhone { get; set; } 
        public string? VoucherCode { get; set; }
        public Guid? VoucherId { get; set; }
        public decimal AmountTendered { get; set; }
        public int PointsUsed { get; set; } = 0;
    }
}
