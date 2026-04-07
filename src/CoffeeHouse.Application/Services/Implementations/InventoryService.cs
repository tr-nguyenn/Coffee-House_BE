using CoffeeHouse.Application.DTOs.Inventory;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Domain.Entities;
using CoffeeHouse.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoffeeHouse.Application.Services.Implementations
{
    public class InventoryService : IInventoryService
    {
        private readonly IUnitOfWork _uow;

        public InventoryService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        // ==========================================
        // 1. NGHIỆP VỤ NHẬP HÀNG
        // ==========================================
        public async Task<bool> ImportStockAsync(Guid materialId, decimal quantity, decimal costPerUnit, string note)
        {
            if (quantity <= 0) throw new Exception("Số lượng nhập phải lớn hơn 0");

            var material = await _uow.Repository<Material>().GetByIdAsync(materialId);
            if (material == null) throw new Exception("Không tìm thấy vật tư này trong kho");

            // Cập nhật tồn kho và giá vốn
            material.StockQuantity += quantity;
            material.CostPerUnit = costPerUnit;

            _uow.Repository<Material>().Update(material);

            // Ghi Log lịch sử kho
            var transaction = new InventoryTransaction
            {
                MaterialId = materialId,
                Type = TransactionType.Import,
                QuantityChanged = quantity,
                RemainingQuantity = material.StockQuantity,
                Note = note ?? "Nhập hàng",
            };

            await _uow.Repository<InventoryTransaction>().AddAsync(transaction);
            await _uow.SaveChangesAsync();

            return true;
        }

        // ==========================================
        // 2. NGHIỆP VỤ TRỪ KHO TỰ ĐỘNG KHI CHỐT ĐƠN
        // ==========================================
        public async Task<bool> DeductStockForOrderAsync(Guid orderId, Dictionary<Guid, int> productQuantities)
        {
            var productIds = productQuantities.Keys.ToList();

            var recipes = await _uow.Repository<ProductRecipe>()
                .GetQueryable()
                .Where(r => productIds.Contains(r.ProductId))
                .Include(r => r.Material)
                .ToListAsync();

            if (!recipes.Any())
                return true;

            var requiredMaterials = new Dictionary<Material, decimal>();

            foreach (var item in productQuantities)
            {
                var productId = item.Key;
                var qtySold = item.Value;

                var productRecipe = recipes.Where(r => r.ProductId == productId).ToList();
                foreach (var recipe in productRecipe)
                {
                    var material = recipe.Material;
                    var totalMaterialNeeded = recipe.Quantity * qtySold;

                    if (requiredMaterials.ContainsKey(material))
                        requiredMaterials[material] += totalMaterialNeeded;
                    else
                        requiredMaterials.Add(material, totalMaterialNeeded);
                }
            }

            foreach (var req in requiredMaterials)
            {
                var material = req.Key;
                var totalDeducted = req.Value;

                material.StockQuantity -= totalDeducted;

                _uow.Repository<Material>().Update(material);

                var transaction = new InventoryTransaction
                {
                    MaterialId = material.Id,
                    Type = TransactionType.Export,
                    QuantityChanged = -totalDeducted,
                    RemainingQuantity = material.StockQuantity,
                    ReferenceId = orderId.ToString(),
                    Note = $"Trừ tự động cho Hóa đơn #{orderId}"
                };
                await _uow.Repository<InventoryTransaction>().AddAsync(transaction);
            }

            await _uow.SaveChangesAsync();
            return true;
        }

        // ==========================================
        // 3. CÀI ĐẶT ĐỊNH LƯỢNG (RECIPE) CHO MÓN
        // ==========================================
        public async Task<bool> SetProductRecipeAsync(Guid productId, List<RecipeItemDto> recipeItems)
        {
            var product = await _uow.Repository<Product>().GetByIdAsync(productId);
            if (product == null) throw new Exception("Không tìm thấy sản phẩm.");

            var recipeRepo = _uow.Repository<ProductRecipe>();

            // 1. Tìm và xóa toàn bộ công thức cũ
            var existingRecipes = await recipeRepo.GetQueryable()
                .Where(r => r.ProductId == productId)
                .ToListAsync();

            if (existingRecipes.Any())
            {
                // Dùng vòng lặp Delete để an toàn 100% với IGenericRepository
                foreach (var recipe in existingRecipes)
                {
                    recipeRepo.Delete(recipe);
                }
            }

            // 2. Thêm công thức mới
            foreach (var item in recipeItems)
            {
                if (item.Quantity <= 0) continue;

                var newRecipe = new ProductRecipe
                {
                    ProductId = productId,
                    MaterialId = item.MaterialId,
                    Quantity = item.Quantity
                };
                await recipeRepo.AddAsync(newRecipe);
            }

            await _uow.SaveChangesAsync();
            return true;
        }
    }
}