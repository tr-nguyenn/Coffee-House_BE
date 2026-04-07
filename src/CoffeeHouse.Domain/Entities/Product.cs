using CoffeeHouse.Domain.Common;

namespace CoffeeHouse.Domain.Entities
{
    public class Product : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsAvailable { get; set; } = true;

        public Guid CategoryId { get; set; }
        public virtual Category Category { get; set; } = null!;

        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public virtual ICollection<ProductRecipe> ProductRecipes { get; set; } = new List<ProductRecipe>();
    }
}
