using CoffeeHouse.Application.DTOs.Vouchers;
using CoffeeHouse.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeHouse.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Staff")]
    public class VouchersController : ControllerBase
    {
        private readonly IVoucherService _voucherService;

        public VouchersController(IVoucherService voucherService)
        {
            _voucherService = voucherService;
        }

        // GET /api/vouchers?search=SALE&pageNumber=1&pageSize=10
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll([FromQuery] VoucherFilterDto filterDto)
        {
            var result = await _voucherService.GetAllPagedAsync(filterDto);
            return Ok(result);
        }

        // GET /api/vouchers/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _voucherService.GetByIdAsync(id);
            if (result == null) return NotFound(new { message = "Không tìm thấy voucher." });
            return Ok(result);
        }

        // POST /api/vouchers (Chỉ Admin)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateVoucherDto dto)
        {
            try
            {
                var result = await _voucherService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // PUT /api/vouchers/{id} (Chỉ Admin)
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVoucherDto dto)
        {
            try
            {
                await _voucherService.UpdateAsync(id, dto);
                return Ok(new { message = "Cập nhật voucher thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // DELETE /api/vouchers/{id} (Chỉ Admin)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _voucherService.DeleteAsync(id);
                return Ok(new { message = "Xóa voucher thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // PUT /api/vouchers/{id}/toggle (Chỉ Admin)
        [HttpPut("{id}/toggle")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleActive(Guid id)
        {
            try
            {
                await _voucherService.ToggleActiveAsync(id);
                return Ok(new { message = "Đã thay đổi trạng thái voucher." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET /api/vouchers/validate?code=SALE50&orderTotal=100000
        [HttpGet("validate")]
        public async Task<IActionResult> Validate([FromQuery] string code, [FromQuery] decimal orderTotal)
        {
            try
            {
                var result = await _voucherService.ValidateVoucherAsync(code, orderTotal);
                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
