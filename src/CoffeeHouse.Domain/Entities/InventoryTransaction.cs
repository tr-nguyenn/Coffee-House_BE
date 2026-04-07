using CoffeeHouse.Domain.Common;
using CoffeeHouse.Domain.Enums;

namespace CoffeeHouse.Domain.Entities
{
    public class InventoryTransaction : BaseEntity
    {
        public Guid MaterialId { get; set; }
        public virtual Material Material { get; set; } = null!;

        public TransactionType Type { get; set; }

        // Số lượng thay đổi (Có thể là số âm hoặc dương)
        public decimal QuantityChanged { get; set; }

        // Cực kỳ quan trọng: Lưu lại Tồn kho ngay tại thời điểm giao dịch này xảy ra
        public decimal RemainingQuantity { get; set; }

        // Lưu vết ID của Hóa đơn bán hàng hoặc Phiếu nhập kho
        public string? ReferenceId { get; set; }

        // Ghi chú (VD: "Bán bill #123", "Nhập hàng nhà cung cấp A")
        public string? Note { get; set; }
    }
}
