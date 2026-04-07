using CoffeeHouse.Application.DTOs.Inventory;

namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface IInventoryService
    {
        // Nhập hàng vào kho bằng tay (Dành cho Quản lý)
        Task<bool> ImportStockAsync(Guid materialId, decimal quantity, decimal costPerUnit, string note);

        // Trừ kho tự động dựa trên hóa đơn bán hàng (Hàm quan trọng nhất)
        // OrderId truyền vào để ghi log, orderDetails chứa danh sách món khách mua
        Task<bool> DeductStockForOrderAsync(Guid orderId, Dictionary<Guid, int> productQuantities);

        //Cài đặt định lượng cho món ăn
        Task<bool> SetProductRecipeAsync(Guid productId, List<RecipeItemDto> recipeItems);

        // Lấy lịch sử xuất nhập kho (Hỗ trợ lọc theo loại, vật tư, thời gian)
        Task<IEnumerable<InventoryTransactionDto>> GetTransactionsAsync(int? type = null, Guid? materialId = null, DateTime? fromDate = null, DateTime? toDate = null);
    }
}
