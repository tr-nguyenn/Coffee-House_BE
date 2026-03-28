namespace CoffeeHouse.Domain.Enums
{
    public enum OrderStatus
    {
        Pending = 0,    // Mới tạo, đang chờ 
        Processing = 1, // Đang pha chế
        Completed = 2,  // Đã thanh toán / Hoàn thành
        Cancelled = 3  // Đã huỷ    
    }
}
