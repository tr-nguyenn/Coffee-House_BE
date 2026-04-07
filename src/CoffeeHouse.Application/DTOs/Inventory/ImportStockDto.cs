namespace CoffeeHouse.Application.DTOs.Inventory
{
    public class ImportStockDto
    {
        public Guid MaterialId { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostPerUnit { get; set; }
        public string? Note { get; set; }
    }
}