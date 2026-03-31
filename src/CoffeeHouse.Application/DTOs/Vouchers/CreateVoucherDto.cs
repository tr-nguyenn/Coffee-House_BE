using CoffeeHouse.Domain.Enums;

namespace CoffeeHouse.Application.DTOs.Vouchers
{
    public class CreateVoucherDto
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DiscountType DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public decimal MinOrderAmount { get; set; } = 0;
        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public int UsageLimit { get; set; } = 100;
    }
}
