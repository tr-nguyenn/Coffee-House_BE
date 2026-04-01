using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using CoffeeHouse.API.Hubs;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Domain.Entities;
using CoffeeHouse.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoffeeHouse.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebhookController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHubContext<PaymentHub> _hubContext;

        public WebhookController(IUnitOfWork unitOfWork, IHubContext<PaymentHub> hubContext)
        {
            _unitOfWork = unitOfWork;
            _hubContext = hubContext; // Tiêm trạm phát sóng SignalR vào đây
        }

        // API này sẽ hứng dữ liệu từ SePay bắn về
        [HttpPost("sepay")]
        public async Task<IActionResult> ReceiveSePayWebhook([FromBody] System.Text.Json.JsonElement payloadElement)
        {
            try
            {
                // Bóc tách dữ liệu một cách an toàn để tránh Model Binding quăng 400 Bad Request
                string transferType = "";
                if (payloadElement.TryGetProperty("transferType", out var typeProp) && typeProp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    transferType = typeProp.GetString() ?? "";
                }

                decimal transferAmount = 0;
                if (payloadElement.TryGetProperty("transferAmount", out var amountProp))
                {
                    if (amountProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                        transferAmount = amountProp.GetDecimal();
                    else if (amountProp.ValueKind == System.Text.Json.JsonValueKind.String && decimal.TryParse(amountProp.GetString(), out var parsedAmt))
                        transferAmount = parsedAmt;
                }

                string content = "";
                if (payloadElement.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    content = contentProp.GetString() ?? "";
                }

                // 1. Bỏ qua nếu là giao dịch trừ tiền (chỉ quan tâm tiền vào)
                if (transferType != "in" || transferAmount <= 0)
                    return Ok(new { success = true, message = "Bỏ qua giao dịch chuyển tiền đi hoặc số tiền không hợp lệ" });

                // 2. Lấy nội dung chuyển khoản (Chuyển thành chữ IN HOA để dễ so sánh)
                var upperContent = content.ToUpper();

                // Lấy các hóa đơn đang chờ thanh toán (Processing)
                var pendingOrders = await _unitOfWork.Repository<Order>().GetQueryable()
                    .Where(o => o.Status == OrderStatus.Processing)
                    .ToListAsync();

                // 3. Tìm xem có mã Hóa Đơn nào nằm lọt thỏm trong Nội dung CK không?
                // Mẹo: Momo và viettelpay thường tự động xoá ký tự gạch ngang '-' trong nội dung CK (VD: ORD-123 thành ORD123)
                // Nên ta cần check cả 2 trường hợp: Có gạch ngang và Không gạch ngang
                var matchedOrder = pendingOrders.FirstOrDefault(o => 
                    upperContent.Contains(o.OrderCode.ToUpper()) || 
                    upperContent.Contains(o.OrderCode.ToUpper().Replace("-", ""))
                );

                if (matchedOrder != null)
                {
                    // 4. Kiểm tra xem khách có chuyển thiếu tiền không?
                    if (transferAmount >= matchedOrder.FinalAmount)
                    {
                        // Khớp 100%! Chốt đơn luôn!
                        matchedOrder.Status = OrderStatus.Completed;
                        matchedOrder.PaymentMethod = "Banking";

                        _unitOfWork.Repository<Order>().Update(matchedOrder);

                        // 👉 TRỌNG TÂM: Rất quan trọng! Giải phóng bàn để màn hình POS tự động báo Trống
                        if (matchedOrder.TableId.HasValue)
                        {
                            var table = await _unitOfWork.Repository<Table>().GetByIdAsync(matchedOrder.TableId.Value);
                            if (table != null)
                            {
                                table.Status = TableStatus.Available;
                                _unitOfWork.Repository<Table>().Update(table);
                            }
                        }

                        await _unitOfWork.SaveChangesAsync();

                        // 5. Bắn tín hiệu "Tia Chớp" xuống màn hình POS VueJS
                        await _hubContext.Clients.All.SendAsync("ReceivePayment", matchedOrder.Id.ToString(), transferAmount);

                        return Ok(new { success = true, message = "Thanh toán thành công & Đã báo cho máy POS" });
                    }
                    else
                    {
                        // Khách chuyển thiếu tiền (Cái này tùy nghiệp vụ, thường thì báo lỗi cho thu ngân biết)
                        return Ok(new { success = true, message = "Khách chuyển thiếu tiền" });
                    }
                }

                return Ok(new { success = true, message = "Không tìm thấy mã đơn hàng trong nội dung" });
            }
            catch (Exception ex)
            {
                // Trả về 200 OK để SePay không bắn lại tin nhắn này nữa
                return Ok(new { success = false, message = ex.Message });
            }
        }
    }

    // Class này dùng để "hứng" cái JSON của SePay
    public class SePayWebhookDto
    {
        public int id { get; set; }
        public string? gateway { get; set; } 
        public string? transactionDate { get; set; } 
        public string? accountNumber { get; set; } 
        public string? subAccount { get; set; }
        public string? code { get; set; } 
        public string? content { get; set; } 
        public string? transferType { get; set; } 
        public decimal transferAmount { get; set; }
        public decimal accumulated { get; set; }
        public string? referenceCode { get; set; }
        public string? description { get; set; }
    }
}