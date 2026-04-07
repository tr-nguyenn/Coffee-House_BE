using CoffeeHouse.Domain.Common;

namespace CoffeeHouse.Domain.Entities
{
    public class ProductRecipe : BaseEntity
    {
        // Món ăn/Thức uống nào?
        public Guid ProductId { get; set; }
        public virtual Product Product { get; set; } = null!;

        // Tiêu hao Nguyên liệu/Vật tư nào?
        public Guid MaterialId { get; set; }
        public virtual Material Material { get; set; } = null!;

        // Mất bao nhiêu lượng? (VD: 20 gram)
        public decimal Quantity { get; set; }
    }
}
