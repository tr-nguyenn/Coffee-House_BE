namespace CoffeeHouse.Application.DTOs.Inventory
{
    public class SetRecipeDto
    {
        public Guid ProductId { get; set; }
        public List<RecipeItemDto> Items { get; set; } = new List<RecipeItemDto>();
    }
}
