using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Orders;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Domain.Entities;
using CoffeeHouse.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoffeeHouse.Application.Services.Implementations
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly IInventoryService _inventoryService;

        public OrderService(IUnitOfWork unitOfWork, IConfiguration configuration, IInventoryService inventoryService)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _inventoryService = inventoryService;
        }

        public async Task<OrderDto> CreateOrderAsync(CreateOrderDto dto, Guid currentStaffId)
        {
            // 1. Kiểm tra đầu vào cơ bản
            if (dto.Items == null || !dto.Items.Any())
                throw new Exception("Đơn hàng phải có ít nhất 1 món!");

            // 2. KIỂM TRA VÀ KHÓA BÀN
            if (dto.TableId.HasValue)
            {
                var table = await _unitOfWork.Repository<Table>().GetByIdAsync(dto.TableId.Value);
                if (table == null) throw new Exception("Không tìm thấy bàn này trong hệ thống.");

                if (table.Status == TableStatus.Occupied)
                    throw new Exception("Bàn này đang có khách ngồi. Vui lòng chọn bàn khác hoặc gọi thêm món!");

                table.Status = TableStatus.Occupied;
                _unitOfWork.Repository<Table>().Update(table);
            }

            // 3. Khởi tạo Hóa đơn
            var order = new Order
            {
                TableId = dto.TableId,
                CustomerId = dto.CustomerId,
                Note = dto.Note,
                PaymentMethod = string.IsNullOrEmpty(dto.PaymentMethod) ? "Cash" : dto.PaymentMethod,
                Status = OrderStatus.Processing,
                CreatedByStaffId = currentStaffId,
                VoucherId = dto.VoucherId,
                PointsUsed = dto.PointsUsed,
                OrderCode = $"ORD-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}"
            };

            decimal totalAmount = 0;

            // 4. Xử lý Từng món ăn
            foreach (var itemDto in dto.Items)
            {
                var product = await _unitOfWork.Repository<Product>().GetByIdAsync(itemDto.ProductId);
                if (product == null) throw new Exception($"Không tìm thấy sản phẩm có ID: {itemDto.ProductId}");

                var orderDetail = new OrderDetail
                {
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = product.Price,
                    Note = itemDto.Note,
                    Status = OrderItemStatus.Processing,
                    PrepStartTime = DateTime.UtcNow
                };

                totalAmount += (orderDetail.Quantity * orderDetail.UnitPrice);
                order.OrderDetails.Add(orderDetail);
            }

            order.TotalAmount = totalAmount;

            // 👉 Biến lưu trữ quá trình tính toán tiền
            decimal finalAmount = totalAmount;
            decimal totalDiscount = 0;

            // ==========================================
            // 5. XỬ LÝ KHÁCH HÀNG (CỘNG & TRỪ ĐIỂM GỘP CHUNG 1 LẦN)
            // ==========================================
            if (dto.CustomerId.HasValue)
            {
                var customer = await _unitOfWork.Repository<Customer>().GetByIdAsync(dto.CustomerId.Value);
                if (customer != null)
                {
                    // 5.1. Xử lý TRỪ ĐIỂM (Giảm giá) trước
                    if (dto.PointsUsed > 0)
                    {
                        if (customer.RewardPoints < dto.PointsUsed)
                            throw new Exception("Khách hàng không đủ điểm tích lũy để sử dụng!");

                        int discountPerPoint = _configuration.GetValue<int>("RewardPolicy:DiscountPerPoint", 1000);
                        decimal pointsDiscount = dto.PointsUsed * discountPerPoint;

                        totalDiscount += pointsDiscount; // 👉 Cộng dồn vào cục giảm giá
                        finalAmount -= pointsDiscount;
                        if (finalAmount < 0) finalAmount = 0;

                        customer.RewardPoints -= dto.PointsUsed; // Trừ điểm trong ví
                    }

                    // 5.2. Xử lý CỘNG ĐIỂM dựa trên số tiền THỰC TRẢ (finalAmount)
                    int amountPerPoint = _configuration.GetValue<int>("RewardPolicy:AmountPerPoint", 10000);
                    int earnedPoints = 0;

                    if (amountPerPoint > 0 && finalAmount > 0)
                    {
                        earnedPoints = (int)(finalAmount / amountPerPoint);
                        customer.RewardPoints += earnedPoints; // Cộng điểm mới
                    }

                    string pointsLog = dto.PointsUsed > 0 ? $" (Đã trừ {dto.PointsUsed} điểm. Tích thêm +{earnedPoints})" : $" (+{earnedPoints} điểm)";
                    order.Note = string.IsNullOrEmpty(order.Note)
                        ? $"Khách: {customer.FullName} - {customer.PhoneNumber}{pointsLog}"
                        : order.Note + $" | Khách: {customer.FullName} - {customer.PhoneNumber}{pointsLog}";

                    _unitOfWork.Repository<Customer>().Update(customer);
                }
            }

            // 6. XỬ LÝ VOUCHER KHUYẾN MÃI (Nếu có)
            if (dto.VoucherId.HasValue)
            {
                var voucher = await _unitOfWork.Repository<Voucher>().GetByIdAsync(dto.VoucherId.Value);
                if (voucher != null && voucher.IsActive && voucher.UsedCount < voucher.UsageLimit)
                {
                    decimal voucherDiscount = 0;
                    if (voucher.DiscountType == DiscountType.FixedAmount)
                    {
                        voucherDiscount = voucher.DiscountValue;
                    }
                    else // Percentage
                    {
                        voucherDiscount = finalAmount * voucher.DiscountValue / 100;
                        if (voucher.MaxDiscountAmount.HasValue && voucherDiscount > voucher.MaxDiscountAmount.Value)
                            voucherDiscount = voucher.MaxDiscountAmount.Value;
                    }

                    totalDiscount += voucherDiscount;
                    finalAmount -= voucherDiscount;
                    if (finalAmount < 0) finalAmount = 0;

                    voucher.UsedCount++;
                    _unitOfWork.Repository<Voucher>().Update(voucher);
                }
            }

            // 7. Chốt tiền và GÁN DISCOUNT XUỐNG DB
            order.DiscountAmount = totalDiscount; // 👉 Gán cục giảm giá cho Entity Framework lưu xuống
            order.FinalAmount = finalAmount < 0 ? 0 : finalAmount;

            // 8. Lưu DB
            await _unitOfWork.Repository<Order>().AddAsync(order);
            await _unitOfWork.SaveChangesAsync();

            return new OrderDto
            {
                Id = order.Id,
                OrderCode = order.OrderCode,
                FinalAmount = order.FinalAmount,
                Status = order.Status.ToString()
            };
        }

        public async Task<OrderDto> GetOrderByIdAsync(Guid orderId)
        {
            var order = await _unitOfWork.Repository<Order>()
                .GetQueryable()
                .AsNoTracking()
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) throw new Exception("Không tìm thấy hóa đơn này.");

            return new OrderDto
            {
                Id = order.Id,
                OrderCode = order.OrderCode,
                FinalAmount = order.FinalAmount,
                Status = order.Status.ToString(),
                OrderDetails = order.OrderDetails.Select(od => new OrderDetailDto
                {
                    Id = od.Id,
                    ProductId = od.ProductId,
                    ProductName = od.Product?.Name ?? "Món ăn",
                    Quantity = od.Quantity,
                    UnitPrice = od.UnitPrice,
                    Note = od.Note
                }).ToList()
            };
        }

        public async Task<OrderDto> AddItemsToOrderAsync(Guid orderId, AddOrderItemsDto dto)
        {
            if (dto.NewItems == null || !dto.NewItems.Any())
                throw new Exception("Chưa chọn món nào để thêm!");

            var order = await _unitOfWork.Repository<Order>()
                .GetQueryable()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) throw new Exception("Không tìm thấy hóa đơn này.");
            if (order.Status != OrderStatus.Processing) throw new Exception("Hóa đơn đã đóng, không thể gọi thêm món!");

            decimal extraAmount = 0;

            foreach (var itemDto in dto.NewItems)
            {
                var product = await _unitOfWork.Repository<Product>().GetByIdAsync(itemDto.ProductId);
                if (product == null) throw new Exception($"Không tìm thấy sản phẩm ID: {itemDto.ProductId}");

                var orderDetail = new OrderDetail
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = product.Price,
                    Note = itemDto.Note,
                    Status = OrderItemStatus.Processing,
                    PrepStartTime = DateTime.UtcNow
                };

                extraAmount += (orderDetail.Quantity * orderDetail.UnitPrice);
                await _unitOfWork.Repository<OrderDetail>().AddAsync(orderDetail);
            }

            order.TotalAmount += extraAmount;
            order.FinalAmount += extraAmount; // Final lúc này tạm bằng Total, giảm giá tính lúc Checkout

            await _unitOfWork.SaveChangesAsync();

            return new OrderDto
            {
                Id = order.Id,
                OrderCode = order.OrderCode,
                FinalAmount = order.FinalAmount,
                Status = order.Status.ToString()
            };
        }

        public async Task<OrderDto> CheckoutOrderAsync(Guid orderId, CheckoutOrderDto dto)
        {
            var order = await _unitOfWork.Repository<Order>()
                .GetQueryable()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) throw new Exception("Không tìm thấy hóa đơn này.");
            if (order.Status != OrderStatus.Processing) throw new Exception("Hóa đơn đã được thanh toán hoặc đã hủy!");

            order.Status = OrderStatus.Completed;
            order.PaymentMethod = string.IsNullOrEmpty(dto.PaymentMethod) ? "Cash" : dto.PaymentMethod;

            // 👉 Biến tính toán tiền nong
            decimal totalDiscount = order.DiscountAmount; // Lấy giảm giá cũ nếu có
            decimal finalAmount = order.TotalAmount;      // Tính lại từ Tổng tiền gốc

            // Tích điểm cho khách hàng có tài khoản
            if (dto.CustomerId.HasValue)
            {
                order.CustomerId = dto.CustomerId;
                var customer = await _unitOfWork.Repository<Customer>().GetByIdAsync(dto.CustomerId.Value);
                if (customer != null)
                {
                    // 1. Trừ điểm ngay lúc thanh toán
                    if (dto.PointsUsed > 0)
                    {
                        if (customer.RewardPoints < dto.PointsUsed)
                            throw new Exception("Khách hàng không đủ điểm tích lũy để sử dụng!");

                        int discountPerPoint = _configuration.GetValue<int>("RewardPolicy:DiscountPerPoint", 1000);
                        decimal pointsDiscount = dto.PointsUsed * discountPerPoint;

                        totalDiscount += pointsDiscount;
                        finalAmount -= pointsDiscount;
                        if (finalAmount < 0) finalAmount = 0;

                        customer.RewardPoints -= dto.PointsUsed;
                        order.PointsUsed = dto.PointsUsed; // Lọc điểm đã dùng vào Hóa đơn
                    }

                    // 2. Tính và cộng điểm thưởng mới
                    int amountPerPoint = _configuration.GetValue<int>("RewardPolicy:AmountPerPoint", 10000);
                    int earnedPoints = 0;
                    if (amountPerPoint > 0 && finalAmount > 0)
                    {
                        earnedPoints = (int)(finalAmount / amountPerPoint);
                        customer.RewardPoints += earnedPoints;
                    }

                    // Cập nhật lại Ghi chú Hóa đơn
                    string pointsLog = dto.PointsUsed > 0 ? $" (Đã trừ {dto.PointsUsed} điểm. Tích thêm +{earnedPoints})" : $" (+{earnedPoints} điểm)";
                    order.Note = string.IsNullOrEmpty(order.Note)
                        ? $"Khách: {customer.FullName} - {customer.PhoneNumber}{pointsLog}"
                        : order.Note + $" | Khách: {customer.FullName} - {customer.PhoneNumber}{pointsLog}";

                    _unitOfWork.Repository<Customer>().Update(customer);
                }
            }
            // Khách vãng lai nhưng cần lưu tên
            else if (!string.IsNullOrEmpty(dto.CustomerName))
            {
                order.Note = string.IsNullOrEmpty(order.Note)
                    ? $"Khách vãng lai: {dto.CustomerName} - {dto.CustomerPhone}"
                    : order.Note + $" | Khách vãng lai: {dto.CustomerName} - {dto.CustomerPhone}";
            }

            // 👉 XỬ LÝ VOUCHER LÚC CHECKOUT (Nếu có)
            if (dto.VoucherId.HasValue)
            {
                order.VoucherId = dto.VoucherId;
                var voucher = await _unitOfWork.Repository<Voucher>().GetByIdAsync(dto.VoucherId.Value);
                if (voucher != null && voucher.IsActive && voucher.UsedCount < voucher.UsageLimit)
                {
                    decimal voucherDiscount = 0;
                    if (voucher.DiscountType == DiscountType.FixedAmount)
                    {
                        voucherDiscount = voucher.DiscountValue;
                    }
                    else // Percentage
                    {
                        voucherDiscount = finalAmount * voucher.DiscountValue / 100;
                        if (voucher.MaxDiscountAmount.HasValue && voucherDiscount > voucher.MaxDiscountAmount.Value)
                            voucherDiscount = voucher.MaxDiscountAmount.Value;
                    }

                    totalDiscount += voucherDiscount;
                    finalAmount -= voucherDiscount;
                    if (finalAmount < 0) finalAmount = 0;

                    voucher.UsedCount++;
                    _unitOfWork.Repository<Voucher>().Update(voucher);
                }
            }

            // 👉 CHỐT TIỀN XUỐNG ENTITY
            order.DiscountAmount = totalDiscount;
            order.FinalAmount = finalAmount < 0 ? 0 : finalAmount;

            _unitOfWork.Repository<Order>().Update(order);

            // Giải phóng bàn về trạng thái Trống
            if (order.TableId.HasValue)
            {
                var table = await _unitOfWork.Repository<Table>().GetByIdAsync(order.TableId.Value);
                if (table != null)
                {
                    table.Status = TableStatus.Available;
                    _unitOfWork.Repository<Table>().Update(table);
                }
            }

            // 👉 TRỪ KHO TỰ ĐỘNG KHI CHECKOUT NGAY TRƯỚC KHI LƯU 
            // Lấy danh sách sản phẩm và số lượng từ OrderDetails hiện có
            var orderDetails = await _unitOfWork.Repository<OrderDetail>()
                .GetQueryable()
                .Where(od => od.OrderId == orderId)
                .ToListAsync();

            var productQuantities = new Dictionary<Guid, int>();
            foreach (var od in orderDetails)
            {
                if (productQuantities.ContainsKey(od.ProductId))
                    productQuantities[od.ProductId] += od.Quantity;
                else
                    productQuantities[od.ProductId] = od.Quantity;
            }

            // Chạy hàm trừ nguyên liệu, nó sẽ tự Include Recipe và log History Lịch sử xuất.
            await _inventoryService.DeductStockForOrderAsync(orderId, productQuantities);

            await _unitOfWork.SaveChangesAsync();

            return new OrderDto
            {
                Id = order.Id,
                OrderCode = order.OrderCode,
                FinalAmount = order.FinalAmount,
                Status = order.Status.ToString()
            };
        }

        public async Task<List<KitchenTicketDto>> GetPendingKitchenItemsAsync()
        {
            var pendingItems = await _unitOfWork.Repository<OrderDetail>()
                .GetQueryable()
                .AsNoTracking()
                .Include(od => od.Product)
                .Include(od => od.Order)
                    .ThenInclude(o => o.Table)
                .Where(od => od.Status == OrderItemStatus.Processing)
                .OrderBy(od => od.PrepStartTime) // FIFO: Ai gọi trước làm trước
                .ToListAsync();

            return pendingItems.Select(od => new KitchenTicketDto
            {
                OrderDetailId = od.Id,
                ProductName = od.Product?.Name ?? "N/A",
                Quantity = od.Quantity,
                Note = od.Note,
                TableName = od.Order?.Table?.Name ?? "Mang về",
                PrepStartTime = od.PrepStartTime
            }).ToList();
        }

        public async Task MarkItemReadyAsync(Guid orderDetailId)
        {
            var orderDetail = await _unitOfWork.Repository<OrderDetail>().GetByIdAsync(orderDetailId);
            if (orderDetail == null) throw new Exception("Không tìm thấy món ăn này.");

            orderDetail.Status = OrderItemStatus.Ready;
            orderDetail.PrepEndTime = DateTime.UtcNow;

            _unitOfWork.Repository<OrderDetail>().Update(orderDetail);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<OrderDto> OpenTableAsync(Guid tableId, Guid currentStaffId)
        {
            var table = await _unitOfWork.Repository<Table>().GetByIdAsync(tableId);
            if (table == null) throw new Exception("Không tìm thấy bàn này.");

            if (table.Status == TableStatus.Occupied)
                throw new Exception("Bàn này đang có khách ngồi rồi, vui lòng chọn bàn khác!");

            var order = new Order
            {
                TableId = tableId,
                Status = OrderStatus.Processing,
                CreatedByStaffId = currentStaffId,
                OrderCode = $"ORD-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                TotalAmount = 0,
                FinalAmount = 0,
                PaymentMethod = "Cash"
            };

            await _unitOfWork.Repository<Order>().AddAsync(order);

            table.Status = TableStatus.Occupied;
            _unitOfWork.Repository<Table>().Update(table);

            await _unitOfWork.SaveChangesAsync();

            return new OrderDto
            {
                Id = order.Id,
                OrderCode = order.OrderCode,
                FinalAmount = order.FinalAmount,
                Status = order.Status.ToString()
            };
        }

        public async Task UpdatePaymentMethodAsync(Guid orderId, string paymentMethod)
        {
            var order = await _unitOfWork.Repository<Order>().GetByIdAsync(orderId);
            if (order == null) throw new Exception("Không tìm thấy đơn hàng.");

            order.PaymentMethod = paymentMethod;
            _unitOfWork.Repository<Order>().Update(order);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<PagedResult<OrderManagementDto>> GetManagementOrdersAsync(OrderFilterDto filter)
        {
            // 1. Khởi tạo Query cơ bản với AsNoTracking để tối ưu hiệu năng đọc (Read-only)
            IQueryable<Order> query = _unitOfWork.Repository<Order>()
                .GetQueryable()
                .AsNoTracking()
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .Include(o => o.Table)
                .Include(o => o.Customer)
                .Include(o => o.CreatedByStaff); // 👉 Include để lấy tên Thu ngân

            // 2. LỌC THEO TỪ KHÓA (Search)
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var searchTerm = filter.Search.Trim().ToLower();
                query = query.Where(o =>
                    o.OrderCode.ToLower().Contains(searchTerm) ||
                    (o.Note != null && o.Note.ToLower().Contains(searchTerm)) ||
                    (o.Customer != null && o.Customer.FullName.ToLower().Contains(searchTerm)) ||
                    (o.Customer != null && o.Customer.PhoneNumber.Contains(searchTerm))
                );
            }

            // 3. LỌC THEO TRẠNG THÁI (Statuses)
            if (filter.Statuses != null && filter.Statuses.Any())
            {
                var statusEnums = filter.Statuses
                    .Select(s => Enum.TryParse<OrderStatus>(s, true, out var parsedStatus) ? parsedStatus : (OrderStatus?)null)
                    .Where(e => e.HasValue)
                    .Select(e => e.Value)
                    .ToList();

                if (statusEnums.Any())
                {
                    query = query.Where(o => statusEnums.Contains(o.Status));
                }
            }

            // 4. LỌC THEO PHƯƠNG THỨC THANH TOÁN
            if (!string.IsNullOrWhiteSpace(filter.PaymentMethod))
            {
                query = query.Where(o => o.PaymentMethod == filter.PaymentMethod);
            }

            // 4.5. LỌC THEO NHÂN VIÊN TẠO ĐƠN
            if (filter.CreatedBy.HasValue)
            {
                query = query.Where(o => o.CreatedByStaffId == filter.CreatedBy.Value);
            }

            // 5. LỌC THEO THỜI GIAN (Đã fix Timezone UTC+7 cho Việt Nam)
            var vnNow = DateTime.UtcNow.AddHours(7);
            var vnToday = vnNow.Date;
            // Chuyển lại về UTC để so sánh với dữ liệu trong DB (CreatedAt lưu UTC)
            var todayUtcStart = vnToday.AddHours(-7);

            switch (filter.TimeRange)
            {
                case "today":
                    query = query.Where(o => o.CreatedAt >= todayUtcStart);
                    break;
                case "yesterday":
                    var yesterdayUtcStart = todayUtcStart.AddDays(-1);
                    query = query.Where(o => o.CreatedAt >= yesterdayUtcStart && o.CreatedAt < todayUtcStart);
                    break;
                case "thisWeek":
                    int diff = (int)vnToday.DayOfWeek - (int)DayOfWeek.Monday;
                    if (diff < 0) diff += 7;
                    var mondayVn = vnToday.AddDays(-diff);
                    var mondayUtcStart = mondayVn.AddHours(-7);
                    query = query.Where(o => o.CreatedAt >= mondayUtcStart);
                    break;
                case "thisMonth":
                    var firstOfMonthVn = new DateTime(vnToday.Year, vnToday.Month, 1);
                    var firstOfMonthUtcStart = firstOfMonthVn.AddHours(-7);
                    query = query.Where(o => o.CreatedAt >= firstOfMonthUtcStart);
                    break;
                case "custom":
                    if (filter.StartDate.HasValue)
                    {
                        var startUtc = filter.StartDate.Value.Date.AddHours(-7);
                        query = query.Where(o => o.CreatedAt >= startUtc);
                    }
                    if (filter.EndDate.HasValue)
                    {
                        // Bao gồm trọn vẹn cả ngày EndDate (đến 23:59:59 giờ VN → chuyển sang UTC)
                        var endUtc = filter.EndDate.Value.Date.AddDays(1).AddHours(-7);
                        query = query.Where(o => o.CreatedAt < endUtc);
                    }
                    break;
            }

            // Đếm tổng số lượng record thỏa mãn bộ lọc (Để phân trang)
            var totalCount = await query.CountAsync();

            // 6. PHÂN TRANG VÀ ÁNH XẠ (DTO PROJECTION)
            var items = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(o => new OrderManagementDto
                {
                    Id = o.Id,
                    OrderCode = o.OrderCode,
                    CreatedAt = o.CreatedAt,
                    TableName = o.Table != null ? o.Table.Name : null,

                    // 👉 Ưu tiên tên từ bảng Customer, nếu không có thì lấy từ Note
                    CustomerName = o.Customer != null ? o.Customer.FullName : null,

                    // 👉 Tên Thu ngân (Nhân viên tạo đơn)
                    CashierName = o.CreatedByStaff != null ? o.CreatedByStaff.FullName : "N/A",

                    TotalAmount = o.TotalAmount,
                    DiscountAmount = o.DiscountAmount,
                    FinalAmount = o.FinalAmount,

                    Status = o.Status.ToString(),
                    PaymentMethod = o.PaymentMethod,
                    OrderDetails = o.OrderDetails.Select(od => new OrderDetailManagementDto
                    {
                        ProductId = od.ProductId,
                        ProductName = od.Product.Name,
                        Quantity = od.Quantity,
                        UnitPrice = od.UnitPrice,
                        Note = od.Note
                    }).ToList()
                })
                .ToListAsync();

            return new PagedResult<OrderManagementDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<byte[]> ExportManagementOrdersToExcelAsync(OrderFilterDto filter)
        {
            // Lấy TẤT CẢ dữ liệu (Không phân trang) bằng cách đặt pageSize cực lớn
            filter.PageNumber = 1;
            filter.PageSize = 100000;
            var result = await GetManagementOrdersAsync(filter);

            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var ws = workbook.Worksheets.Add("Hóa đơn");

            // === HEADER ROW ===
            var headers = new[] { "STT", "Mã HĐ", "Ngày tạo", "Bàn", "Khách hàng", "Thu ngân", "Tổng gốc", "Giảm giá", "Thực thu", "Trạng thái", "Thanh toán" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
            }

            // Style header
            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#1F4E79");
            headerRange.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            headerRange.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
            headerRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;

            // === DATA ROWS ===
            var statusMap = new Dictionary<string, string>
            {
                { "Completed", "Hoàn thành" },
                { "Processing", "Đang phục vụ" },
                { "Cancelled", "Đã hủy" }
            };
            var paymentMap = new Dictionary<string, string>
            {
                { "Cash", "Tiền mặt" },
                { "Banking", "Chuyển khoản / QR" },
                { "Card", "Thẻ / Ví" }
            };

            int row = 2;
            int stt = 1;
            foreach (var order in result.Items)
            {
                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = order.OrderCode;
                ws.Cell(row, 3).Value = order.CreatedAt.AddHours(7); // Hiển thị giờ VN
                ws.Cell(row, 3).Style.NumberFormat.Format = "dd/MM/yyyy HH:mm";
                ws.Cell(row, 4).Value = order.TableName ?? "Mang đi";
                ws.Cell(row, 5).Value = order.CustomerName ?? "Khách lẻ";
                ws.Cell(row, 6).Value = order.CashierName ?? "N/A";
                ws.Cell(row, 7).Value = order.TotalAmount;
                ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 8).Value = order.DiscountAmount;
                ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 9).Value = order.FinalAmount;
                ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 10).Value = statusMap.GetValueOrDefault(order.Status, order.Status);
                ws.Cell(row, 11).Value = paymentMap.GetValueOrDefault(order.PaymentMethod ?? "", order.PaymentMethod ?? "");

                // Tô màu xen kẽ dòng cho dễ đọc
                if (row % 2 == 0)
                {
                    ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#F2F7FB");
                }
                row++;
            }

            // Viền cho toàn bộ dữ liệu
            if (result.Items.Any())
            {
                var dataRange = ws.Range(1, 1, row - 1, headers.Length);
                dataRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            }

            // Auto-fit cột
            ws.Columns().AdjustToContents();

            // === DÒNG TỔNG KẾT ===
            row++;
            ws.Cell(row, 6).Value = "TỔNG CỘNG:";
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 7).FormulaA1 = $"SUM(G2:G{row - 2})";
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 7).Style.Font.Bold = true;
            ws.Cell(row, 8).FormulaA1 = $"SUM(H2:H{row - 2})";
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 8).Style.Font.Bold = true;
            ws.Cell(row, 9).FormulaA1 = $"SUM(I2:I{row - 2})";
            ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 9).Style.Font.Bold = true;
            ws.Cell(row, 9).Style.Font.FontColor = ClosedXML.Excel.XLColor.Red;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}