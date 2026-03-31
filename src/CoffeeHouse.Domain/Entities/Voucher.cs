using CoffeeHouse.Domain.Common;
using CoffeeHouse.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeHouse.Domain.Entities
{
    public class Voucher : BaseEntity
    {
       public string Code { get; set; } = string.Empty; // VD: SALE50, MUNG8THANG3
        public string Description { get; set; } = string.Empty; 
        
        public DiscountType DiscountType { get; set; } 
        public decimal DiscountValue { get; set; } // Giá trị giảm (số tiền hoặc số %)
        public decimal? MaxDiscountAmount { get; set; } // Giảm tối đa bao nhiêu tiền (nếu là % mới dùng)
        public decimal MinOrderAmount { get; set; } = 0; // Đơn từ bao nhiêu tiền mới được áp dụng
        
        public DateTime StartDate { get; set; } 
        public DateTime ExpiryDate { get; set; } 
        
        public int UsageLimit { get; set; } = 1; // Tổng số lượng mã phát hành
        public int UsedCount { get; set; } = 0;  // Số lượt đã xài
        
        public bool IsActive { get; set; } = true; // Trạng thái bật/tắt mã

        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
