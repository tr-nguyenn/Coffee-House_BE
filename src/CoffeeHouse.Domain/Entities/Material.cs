using CoffeeHouse.Domain.Common;

namespace CoffeeHouse.Domain.Entities
{
    public class Material : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        // Đơn vị tính (VD: gram, ml, chai, cái, kg...)
        public string Unit { get; set; } = string.Empty;

        // Số lượng tồn kho hiện tại
        public decimal StockQuantity { get; set; } = 0;

        // Mức cảnh báo sắp hết (VD: còn 500g thì báo động)
        public decimal MinStockLevel { get; set; } = 0;

        // Giá vốn nhập hàng trung bình (Để sau này tính lợi nhuận gộp)
        public decimal CostPerUnit { get; set; } = 0;

        // Navigation properties
        public virtual ICollection<ProductRecipe> ProductRecipes { get; set; } = new List<ProductRecipe>();
        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    }
}
