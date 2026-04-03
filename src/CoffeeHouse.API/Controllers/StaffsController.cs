using CoffeeHouse.Application.DTOs.Staffs;
using CoffeeHouse.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeHouse.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")] 
    public class StaffsController : ControllerBase
    {
        private readonly IStaffService _staffService;

        public StaffsController(IStaffService staffService)
        {
            _staffService = staffService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] StaffFilterDto filterDto)
        {
            return Ok(await _staffService.GetAllPagedAsync(filterDto));
        }

        // 👉 Endpoint nhẹ: Trả về danh sách (Id, FullName) cho Dropdown lọc ở trang Quản lý hóa đơn
        [HttpGet("simple-list")]
        public async Task<IActionResult> GetSimpleList()
        {
            var allStaff = await _staffService.GetAllPagedAsync(new StaffFilterDto { PageSize = 500 });
            var simpleList = allStaff.Items.Select(s => new { s.Id, Name = s.FullName }).ToList();
            return Ok(simpleList);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _staffService.GetByIdAsync(id);
            if (result == null) return NotFound(new { message = "Nỏ tìm thấy nhân viên." });
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateStaffDto dto)
        {
            var result = await _staffService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStaffDto dto)
        {
            await _staffService.UpdateAsync(id, dto);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _staffService.DeleteAsync(id);
            return NoContent();
        }
    }
}