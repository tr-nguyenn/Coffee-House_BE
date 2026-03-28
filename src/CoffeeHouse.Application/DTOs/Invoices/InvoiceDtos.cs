using CoffeeHouse.Application.Common;

namespace CoffeeHouse.Application.DTOs.Invoices
{
    public  class InvoiceDtos : BaseFilterDto
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
    public class InvoiceFilterDto : BaseFilterDto
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class InvoiceDto
    {
        public Guid Id { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public DateTime CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public decimal FinalAmount { get; set; }
        public string Status { get; set; } = "Đã thanh toán";
    }

    public class InvoicePagedResult : PagedResult<InvoiceDto>
    {
        public decimal TotalRevenue { get; set; }
    }

    public class InvoiceDetailDto : InvoiceDto
    {
        public decimal SubTotal { get; set; }
        public decimal Discount { get; set; }
        public List<InvoiceItemDto> Items { get; set; } = new();
    }
    public class InvoiceItemDto
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice => Quantity * UnitPrice;
    }
}
