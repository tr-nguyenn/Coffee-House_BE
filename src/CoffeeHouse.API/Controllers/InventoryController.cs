using CoffeeHouse.Application.DTOs.Inventory;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeHouse.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Staff")]
    public class InventoryController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IInventoryService _inventoryService;

        public InventoryController(IUnitOfWork uow, IInventoryService inventoryService)
        {
            _uow = uow;
            _inventoryService = inventoryService;
        }

        [HttpGet("materials")]
        public async Task<IActionResult> GetMaterials()
        {
            var materials = await _uow.Repository<Material>()
                .GetQueryable()
                .OrderBy(m => m.Name)
                .Select(m => new MaterialDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Unit = m.Unit,
                    StockQuantity = m.StockQuantity,
                    MinStockLevel = m.MinStockLevel,
                    CostPerUnit = m.CostPerUnit
                })
                .ToListAsync();

            return Ok(materials);
        }

        [HttpPost("materials")]
        public async Task<IActionResult> CreateMaterial([FromBody] MaterialDto dto)
        {
            var newMaterial = new Material
            {
                Name = dto.Name,
                Unit = dto.Unit,
                MinStockLevel = dto.MinStockLevel,
                StockQuantity = 0,
                CostPerUnit = 0
            };

            await _uow.Repository<Material>().AddAsync(newMaterial);
            await _uow.SaveChangesAsync();

            return Ok(new { message = "Thêm vật tư thành công", id = newMaterial.Id });
        }

        [HttpPost("import")]
        public async Task<IActionResult> ImportStock([FromBody] ImportStockDto dto)
        {
            try
            {
                var result = await _inventoryService.ImportStockAsync(
                    dto.MaterialId,
                    dto.Quantity,
                    dto.CostPerUnit,
                    dto.Note ?? "Nhập hàng từ hệ thống"
                );

                if (result) return Ok(new { message = "Nhập kho thành công!" });
                return BadRequest("Nhập kho thất bại.");
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // 👉 ĐÃ THÊM: API lấy định lượng (Chống lỗi 404)
        [HttpGet("recipes/{productId}")]
        public async Task<IActionResult> GetProductRecipes(Guid productId)
        {
            var recipes = await _uow.Repository<ProductRecipe>()
                .GetQueryable()
                .Include(r => r.Material)
                .Where(r => r.ProductId == productId)
                .Select(r => new
                {
                    r.MaterialId,
                    MaterialName = r.Material.Name,
                    r.Quantity,
                    Unit = r.Material.Unit
                })
                .ToListAsync();

            return Ok(recipes);
        }

        // 👉 ĐÃ THÊM: API lưu định lượng
        [HttpPost("recipes")]
        public async Task<IActionResult> SetProductRecipe([FromBody] SetRecipeDto dto)
        {
            try
            {
                var result = await _inventoryService.SetProductRecipeAsync(dto.ProductId, dto.Items);
                if (result)
                    return Ok(new { message = "Cập nhật định lượng thành công!" });

                return BadRequest("Cập nhật định lượng thất bại.");
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("materials/{id}")]
        public async Task<IActionResult> UpdateMaterial(Guid id, [FromBody] MaterialDto dto)
        {
            var material = await _uow.Repository<Material>().GetByIdAsync(id);
            if (material == null) return NotFound("Không tìm thấy vật tư");

            material.Name = dto.Name;
            material.Unit = dto.Unit;
            material.MinStockLevel = dto.MinStockLevel;

            _uow.Repository<Material>().Update(material);
            await _uow.SaveChangesAsync();

            return Ok(new { message = "Cập nhật thành công" });
        }
    }
}