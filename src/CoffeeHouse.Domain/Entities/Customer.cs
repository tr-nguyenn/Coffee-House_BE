using CoffeeHouse.Domain.Common;

namespace CoffeeHouse.Domain.Entities
{
    public class Customer : BaseEntity
    {
        public string IdentityId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public int RewardPoints { get; set; } = 0;
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
