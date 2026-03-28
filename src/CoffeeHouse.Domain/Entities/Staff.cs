using CoffeeHouse.Domain.Common;
using CoffeeHouse.Domain.Enums;

namespace CoffeeHouse.Domain.Entities
{
    public class Staff : BaseEntity
    {
        public string IdentityId { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;
        public StaffStatus Status { get; set; } = StaffStatus.Active;
        public DateTime HireDate { get; set; }

        // Mối quan hệ: 1 Nhân viên tạo nhiều Hóa đơn
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
