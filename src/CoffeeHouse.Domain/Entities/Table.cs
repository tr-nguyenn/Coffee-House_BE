using CoffeeHouse.Domain.Common;
using CoffeeHouse.Domain.Enums;

namespace CoffeeHouse.Domain.Entities
{
    public class Table : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Capacity { get; set; } = 4;
        public TableStatus Status { get; set; } = TableStatus.Available;
        public int DisplayOrder { get; set; } = 0;

        // Khóa ngoại trỏ về bảng Area
        public Guid AreaId { get; set; }
        public virtual Area Area { get; set; } = null!;
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
