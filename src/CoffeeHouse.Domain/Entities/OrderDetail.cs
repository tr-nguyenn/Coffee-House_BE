using CoffeeHouse.Domain.Common;
using CoffeeHouse.Domain.Enums;

namespace CoffeeHouse.Domain.Entities
{
    public class OrderDetail : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Guid ProductId { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        public string? Note { get; set; } // "Ít đá, nhiều sữa"

        // Mặc định sinh ra là Processing luôn
        public OrderItemStatus Status { get; set; } = OrderItemStatus.Processing;

        // --- ĐO LƯỜNG TỐC ĐỘ BẾP ---
        // Sẽ được gán = DateTime.UtcNow NGAY LÚC TẠO ĐƠN HÀNG
        public DateTime PrepStartTime { get; set; }

        // Sẽ được gán = DateTime.UtcNow KHI BẾP BẤM NÚT "XONG"
        public DateTime? PrepEndTime { get; set; }

        // Navigation properties
        public virtual Order Order { get; set; } = null!;
        public virtual Product Product { get; set; } = null!;
    }
}