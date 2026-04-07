using CoffeeHouse.Domain.Enums;

namespace CoffeeHouse.Application.DTOs.Inventory
{
    public class InventoryTransactionDto
    {
        public Guid Id { get; set; }
        public Guid MaterialId { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
        public string TypeName => Type.ToString(); 
        public decimal QuantityChanged { get; set; }
        public decimal RemainingQuantity { get; set; }
        public string? ReferenceId { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
