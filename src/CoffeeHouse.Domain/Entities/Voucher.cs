using CoffeeHouse.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeHouse.Domain.Entities
{
    public class Voucher : BaseEntity
    {
        public string Code { get; set; } = string.Empty; 
        [Column(TypeName = "decimal(18,2)")]
        public decimal? DiscountPercent { get; set; } // Giảm theo %
        [Column(TypeName = "decimal(18,2)")]
        public decimal? DiscountAmount { get; set; }  // Giảm tiền mặt
        public int PointsRequired { get; set; } = 0;
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; } = true;

        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
