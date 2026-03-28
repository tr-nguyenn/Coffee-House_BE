namespace CoffeeHouse.Domain.Enums
{
    public enum OrderItemStatus
    {
        Processing = 0, // Vừa tạo order là món nhảy vô đây, đồng hồ đếm ngược bắt đầu chạy luôn
        Ready = 1,      // Bếp làm xong, chọt nút "Xong" -> dừng đồng hồ, báo cho Phục vụ
        Delivered = 2,  // Phục vụ đã mang ra bàn cho khách
        Cancelled = 3   // Hủy món (Hết nguyên liệu hoặc khách đổi ý)
    }
}
