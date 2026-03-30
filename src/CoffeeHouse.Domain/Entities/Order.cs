using CoffeeHouse.Domain.Common;
using CoffeeHouse.Domain.Enums;
using CoffeeHouse.Infrastructure;

namespace CoffeeHouse.Domain.Entities
{
    public class Order : BaseEntity
    {
        public string OrderCode { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Processing;

        public string PaymentMethod { get; set; } = "Cash";

        public string? Note { get; set; }

        public int PointsEarned { get; set; } = 0;
        public int PointsUsed { get; set; } = 0;
        public decimal DiscountAmount { get; set; } = 0;
        public decimal FinalAmount { get; set; }

        public Guid? CustomerId { get; set; }
        public virtual Customer? Customer { get; set; }
        public Guid? TableId { get; set; }
        public virtual Table? Table { get; set; }

        public Guid? VoucherId { get; set; }
        public virtual Voucher? Voucher { get; set; }

        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

        public Guid CreatedByStaffId { get; set; }
        public virtual ApplicationUser CreatedByStaff { get; set; } = null!;
    }
}