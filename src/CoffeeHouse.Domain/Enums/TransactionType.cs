namespace CoffeeHouse.Domain.Enums
{
    public enum TransactionType
    {
        Import = 1,       // Nhập hàng vào kho (Cộng số lượng)
        Export = 2,       // Xuất kho do bán hàng (Trừ số lượng)
        Adjustment = 3,   // Kiểm kê/Điều chỉnh (Cộng/Trừ do hao hụt, hư hỏng)
        Return = 4        // Khách trả hàng (Cộng lại vào kho - ít dùng nhưng nên có)
    }
}
