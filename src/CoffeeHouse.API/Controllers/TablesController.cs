using CoffeeHouse.Application.DTOs.Tables;
using CoffeeHouse.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeHouse.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Staff")]
    public class TablesController : ControllerBase
    {
        private readonly ITableService _tableService;

        public TablesController(ITableService tableService)
        {
            _tableService = tableService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] TableFilterDto filterDto)
        {
            var result = await _tableService.GetAllPagedAsync(filterDto);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _tableService.GetByIdAsync(id);
            if (result == null) return NotFound(new { message = "Không tìm thấy bàn này." });
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTableDto dto)
        {
            var result = await _tableService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTableDto dto)
        {
            await _tableService.UpdateAsync(id, dto);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _tableService.DeleteAsync(id);
            return NoContent();
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetTablesWithStatus()
        {
            try
            {
                var result = await _tableService.GetTablesWithStatusAsync();
                return Ok(result); // Trả về HTTP 200 kèm danh sách bàn
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
