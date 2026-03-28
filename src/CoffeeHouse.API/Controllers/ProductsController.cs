using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Products;
using CoffeeHouse.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeHouse.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Staff")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] ProductFilterDto filterDto)
        {
            var result = await _productService.GetAllPagedAsync(filterDto);
            return Ok(ApiResponse<PagedResult<ProductDto>>.SuccessResult(result));
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _productService.GetByIdAsync(id);
            if (result == null) return NotFound(ApiResponse<string>.FailureResult("Không tìm thấy sản phẩm."));

            return Ok(ApiResponse<ProductDto>.SuccessResult(result));
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Create([FromForm] CreateUpdateProductDto dto)
        {
            var result = await _productService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                   ApiResponse<ProductDto>.SuccessResult(result, "Thêm sản phẩm thành công!"));
        }

        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Update(Guid id, [FromForm] CreateUpdateProductDto dto)
        {
            await _productService.UpdateAsync(id, dto);
            return Ok(ApiResponse<string>.SuccessResult("Cập nhật thành công."));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _productService.DeleteAsync(id);
            return Ok(ApiResponse<string>.SuccessResult("Xóa sản phẩm thành công."));
        }
    }
}
