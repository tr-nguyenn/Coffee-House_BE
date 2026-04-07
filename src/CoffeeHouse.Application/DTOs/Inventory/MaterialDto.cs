namespace CoffeeHouse.Application.DTOs.Inventory
{
    public class MaterialDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal StockQuantity { get; set; }
        public decimal MinStockLevel { get; set; }
        public decimal CostPerUnit { get; set; }
    }
}