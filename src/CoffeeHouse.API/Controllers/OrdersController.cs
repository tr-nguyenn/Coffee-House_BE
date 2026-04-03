using CoffeeHouse.Application.DTOs.Orders;
using CoffeeHouse.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CoffeeHouse.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            try
            {
                // Lấy StaffId từ JWT Token của người đang đăng nhập
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    return Unauthorized(new { message = "Không xác định được danh tính nhân viên." });
                }

                var currentStaffId = Guid.Parse(userIdClaim.Value);

                // Gọi Service xử lý nghiệp vụ tạo đơn hàng
                var result = await _orderService.CreateOrderAsync(dto, currentStaffId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                // Trả về lỗi 400 Bad Request nếu có lỗi logic (ví dụ: thiếu món, sai giá...)
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id:guid}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> GetOrderById(Guid id)
        {
            try
            {
                var result = await _orderService.GetOrderByIdAsync(id);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}/add-items")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> AddItemsToOrder(Guid id, [FromBody] AddOrderItemsDto dto)
        {
            try
            {
                var result = await _orderService.AddItemsToOrderAsync(id, dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}/checkout")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> CheckoutOrder(Guid id, [FromBody] CheckoutOrderDto dto)
        {
            try
            {
                var result = await _orderService.CheckoutOrderAsync(id, dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("kitchen-tickets")]
        [Authorize(Roles = "Admin,Staff, Kitchen")]
        public async Task<IActionResult> GetKitchenTickets()
        {
            try
            {
                var result = await _orderService.GetPendingKitchenItemsAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("items/{itemId}/ready")]
        public async Task<IActionResult> MarkItemReady(Guid itemId)
        {
            try
            {
                await _orderService.MarkItemReadyAsync(itemId);
                return Ok(new { message = "Món ăn đã sẵn sàng phục vụ!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("open-table/{tableId}")]
        public async Task<IActionResult> OpenTable(Guid tableId)
        {
            try
            {
                // Lấy ID của nhân viên đang đăng nhập từ Token (JWT)
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userIdString))
                    return Unauthorized(new { message = "Vui lòng đăng nhập lại!" });

                Guid staffId = Guid.Parse(userIdString);

                var result = await _orderService.OpenTableAsync(tableId, staffId);
                return Ok(new { message = "Mở bàn thành công!", data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}/payment-method")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> UpdatePaymentMethod(Guid id, [FromBody] string paymentMethod)
        {
            try
            {
                await _orderService.UpdatePaymentMethodAsync(id, paymentMethod);
                return Ok(new { message = "Đã cập nhật phương thức thanh toán" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("management")]
        public async Task<IActionResult> GetManagementOrders([FromQuery] OrderFilterDto filter)
        {
            try
            {
                var result = await _orderService.GetManagementOrdersAsync(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("management/export-excel")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportManagementOrdersToExcel([FromQuery] OrderFilterDto filter)
        {
            try
            {
                var fileBytes = await _orderService.ExportManagementOrdersToExcelAsync(filter);
                var fileName = $"HoaDon_{DateTime.UtcNow.AddHours(7):yyyyMMdd_HHmmss}.xlsx";
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}