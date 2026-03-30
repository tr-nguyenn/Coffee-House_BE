

using CoffeeHouse.Domain.Common;

namespace CoffeeHouse.Domain.Entities
{
    public class Area : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public int DisplayOrder { get; set; } = 0;
        public virtual ICollection<Table> Tables { get; set; } = new List<Table>();
    }
}
