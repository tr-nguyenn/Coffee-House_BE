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

        public OrderService(IUnitOfWork unitOfWork, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
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
                .Include(o => o.Customer); // 👉 Đã có bảng Customer để lấy thông tin

            // 2. LỌC THEO TỪ KHÓA (Search)
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var searchTerm = filter.Search.Trim().ToLower();
                // 👉 ĐÃ FIX: Mở rộng vùng tìm kiếm sang cả Tên và SĐT của Khách hàng trong DB
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
                // 👉 ĐÃ FIX TỐI ƯU EF CORE: Ép kiểu chuỗi sang Enum trước khi query để tránh lỗi "Translation Failed"
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

            // 5. LỌC THEO THỜI GIAN
            var today = DateTime.UtcNow.Date;
            switch (filter.TimeRange)
            {
                case "today":
                    query = query.Where(o => o.CreatedAt >= today);
                    break;
                case "yesterday":
                    var yesterday = today.AddDays(-1);
                    query = query.Where(o => o.CreatedAt >= yesterday && o.CreatedAt < today);
                    break;
                case "thisWeek":
                    var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
                    query = query.Where(o => o.CreatedAt >= startOfWeek);
                    break;
                case "thisMonth":
                    var startOfMonth = new DateTime(today.Year, today.Month, 1);
                    query = query.Where(o => o.CreatedAt >= startOfMonth);
                    break;
                case "custom":
                    if (filter.StartDate.HasValue)
                        query = query.Where(o => o.CreatedAt >= filter.StartDate.Value.Date);
                    if (filter.EndDate.HasValue)
                    {
                        // Bao gồm trọn vẹn cả ngày EndDate (đến 23:59:59)
                        var endOfDay = filter.EndDate.Value.Date.AddDays(1).AddTicks(-1);
                        query = query.Where(o => o.CreatedAt <= endOfDay);
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

                    // 👉 ĐÃ FIX: Ưu tiên bốc tên từ bảng Customer (khách quen), nếu không có mới móc trong Note (Khách vãng lai)
                    CustomerName = o.Customer != null ? o.Customer.FullName : o.Note,

                    // 👉 ĐÃ FIX: Trả đủ 3 cục tiền để Frontend vẽ lên bảng
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
    }
}