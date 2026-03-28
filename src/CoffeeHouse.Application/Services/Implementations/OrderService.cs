using CoffeeHouse.Application.DTOs.Orders;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Domain.Entities;
using CoffeeHouse.Domain.Enums;
using Microsoft.EntityFrameworkCore;


namespace CoffeeHouse.Application.Services.Implementations
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;

        public OrderService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<OrderDto> CreateOrderAsync(CreateOrderDto dto, Guid currentStaffId)
        {
            if (dto.Items == null || !dto.Items.Any())
                throw new Exception("Đơn hàng phải có ít nhất 1 món!");

            // 1. Tạo entity Order chính
            var order = new Order
            {
                TableId = dto.TableId,
                UserId = dto.UserId,
                Note = dto.Note,
                PaymentMethod = dto.PaymentMethod,
                Status = OrderStatus.Processing, 
                CreatedByStaffId = currentStaffId,
                VoucherId = dto.VoucherId,
                PointsUsed = dto.PointsUsed,
                OrderCode = $"ORD-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}"
            };

            decimal totalAmount = 0;

            // 2. Xử lý từng món ăn (OrderDetails)
            foreach (var itemDto in dto.Items)
            {
                // Lấy giá chuẩn từ Database để chống hack từ Frontend
                var product = await _unitOfWork.Repository<Product>().GetByIdAsync(itemDto.ProductId);
                if (product == null) throw new Exception($"Không tìm thấy sản phẩm có ID: {itemDto.ProductId}");

                var orderDetail = new OrderDetail
                {
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = product.Price,
                    Note = itemDto.Note,

                    // --- PHÉP MÀU CỦA KDS BẮT ĐẦU Ở ĐÂY ---
                    Status = OrderItemStatus.Processing, // Món bắt đầu vào trạng thái đang làm
                    PrepStartTime = DateTime.UtcNow      // ĐỒNG HỒ BẤM GIỜ BẮT ĐẦU CHẠY!
                };

                // Cộng dồn tiền
                totalAmount += (orderDetail.Quantity * orderDetail.UnitPrice);

                order.OrderDetails.Add(orderDetail);
            }

            // 3. Tính toán tiền nong (Tạm thời Final = Total, phần Voucher tính sau)
            order.TotalAmount = totalAmount;
            order.FinalAmount = totalAmount;
            // Nếu có Voucher thì viết thêm hàm trừ tiền ở đây...

            // 4. Lưu toàn bộ xuống DB trong 1 Transaction
            await _unitOfWork.Repository<Order>().AddAsync(order);

            // 5. Nếu có TableId thì cập nhật trạng thái Bàn thành Đỏ (Đang có khách)
            if (dto.TableId.HasValue)
            {
                var table = await _unitOfWork.Repository<Table>().GetByIdAsync(dto.TableId.Value);
                if (table != null)
                {
                    table.Status = TableStatus.Occupied;
                    _unitOfWork.Repository<Table>().Update(table);
                }
            }

            await _unitOfWork.SaveChangesAsync();

            // Tạm thời trả về object cơ bản, sau này mi xài AutoMapper map ra cho đẹp
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
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) throw new Exception("Không tìm thấy hóa đơn này.");

            var dto = new OrderDto
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

            return dto;
        }

        public async Task<List<TableStatusDto>> GetTablesWithStatusAsync()
        {
            // Lấy tất cả bàn, include luôn Khu vực (Area) và các Đơn hàng (Order) của bàn đó
            var tables = await _unitOfWork.Repository<Table>()
                .GetQueryable()
                .Include(t => t.Area)
                .Include(t => t.Orders.Where(o => o.Status == OrderStatus.Processing)) // Chỉ lấy Bill chưa thanh toán
                .ToListAsync();

            var result = tables.Select(t => {
                var activeOrder = t.Orders.FirstOrDefault(); // Lấy cái bill đang mở (nếu có)
                return new TableStatusDto
                {
                    TableId = t.Id,
                    TableName = t.Name,
                    AreaName = t.Area != null ? t.Area.Name : "N/A",
                    IsInUse = activeOrder != null, // Có bill đang mở -> Đang dùng (Màu Đỏ)
                    ActiveOrderId = activeOrder?.Id,
                    ActiveOrderCode = activeOrder?.OrderCode
                };
            }).ToList();

            return result;
        }

        public async Task<OrderDto> AddItemsToOrderAsync(Guid orderId, AddOrderItemsDto dto)
        {
            if (dto.NewItems == null || !dto.NewItems.Any())
                throw new Exception("Chưa chọn món nào để thêm!");

            // 1. Tìm cái Hóa đơn đang mở lên
            var order = await _unitOfWork.Repository<Order>()
                .GetQueryable()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) throw new Exception("Không tìm thấy hóa đơn này.");
            if (order.Status != OrderStatus.Processing) throw new Exception("Hóa đơn này đã đóng, không thể gọi thêm món!");

            decimal extraAmount = 0;

            // 2. Xử lý các món gọi thêm
            foreach (var itemDto in dto.NewItems)
            {
                var product = await _unitOfWork.Repository<Product>().GetByIdAsync(itemDto.ProductId);
                if (product == null) throw new Exception($"Không tìm thấy sản phẩm ID: {itemDto.ProductId}");

                var orderDetail = new OrderDetail
                {
                    Id = Guid.NewGuid(), // 👉 FIX 1: Tự sinh ID cứng luôn để ép EF Core phải hiểu đây là món MỚI (INSERT)
                    OrderId = order.Id,  // 👉 FIX 2: Gắn chặt ID của cái Bill hiện tại vào món này
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = product.Price,
                    Note = itemDto.Note,
                    Status = OrderItemStatus.Processing,
                    PrepStartTime = DateTime.UtcNow
                };

                extraAmount += (orderDetail.Quantity * orderDetail.UnitPrice);

                // 👉 FIX 3: Add trực tiếp vào bảng OrderDetails (Ép chạy lệnh INSERT an toàn 100%)
                await _unitOfWork.Repository<OrderDetail>().AddAsync(orderDetail);
            }

            // 3. Cập nhật lại tổng tiền của Bill
            order.TotalAmount += extraAmount;
            order.FinalAmount += extraAmount;

            // ⚠️ LƯU Ý: Tuyệt đối KHÔNG GỌI hàm Update(order) ở đây. EF Core tự biết TotalAmount đã thay đổi.

            // 4. Lưu đồng loạt xuống Database
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
            // 1. Tìm Hóa đơn đang mở
            var order = await _unitOfWork.Repository<Order>()
                .GetQueryable()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) throw new Exception("Không tìm thấy hóa đơn này.");
            if (order.Status != OrderStatus.Processing) throw new Exception("Hóa đơn này đã được thanh toán hoặc đã hủy!");

            // 2. Chốt Hóa đơn & Ghi nhận Phương thức thanh toán
            order.Status = OrderStatus.Completed;

            // Nếu FE không truyền xuống thì mặc định là Tiền mặt (Cash)
            order.PaymentMethod = string.IsNullOrEmpty(dto.PaymentMethod) ? "Cash" : dto.PaymentMethod;

            // ==========================================
            // 3. LOGIC XỬ LÝ KHÁCH HÀNG & TÍCH ĐIỂM
            // ==========================================
            if (dto.CustomerId.HasValue)
            {
                // Gắn ID khách hàng vào Bill để sau này thống kê (Lưu ý: Nếu bảng Order mi dùng tên là UserId thì sửa lại chữ UserId nhé)
                order.UserId = dto.CustomerId;

                // Tìm khách hàng trong DB để cộng điểm
                var customer = await _unitOfWork.Repository<Customer>().GetByIdAsync(dto.CustomerId.Value);
                if (customer != null)
                {
                    // Công thức: 10.000đ = 1 điểm. (Dùng ép kiểu int để lấy phần nguyên, ví dụ 55k / 10k = 5 điểm)
                    int earnedPoints = (int)(order.FinalAmount / 10000);

                    customer.RewardPoints += earnedPoints; // Cộng điểm vào tài khoản

                    _unitOfWork.Repository<Customer>().Update(customer);
                }
            }
            else if (!string.IsNullOrEmpty(dto.CustomerName))
            {
                // Trường hợp khách vãng lai (Không có trong DB) nhưng vẫn muốn in tên lên Bill
                order.Note = string.IsNullOrEmpty(order.Note)
                    ? $"Khách vãng lai: {dto.CustomerName} - {dto.CustomerPhone}"
                    : order.Note + $" | Khách vãng lai: {dto.CustomerName} - {dto.CustomerPhone}";
            }
            // ==========================================

            _unitOfWork.Repository<Order>().Update(order);

            // 4. Giải phóng Bàn (Trả về màu Xanh)
            if (order.TableId.HasValue)
            {
                var table = await _unitOfWork.Repository<Table>().GetByIdAsync(order.TableId.Value);
                if (table != null)
                {
                    table.Status = TableStatus.Available; // Đổi bàn thành Trống
                    _unitOfWork.Repository<Table>().Update(table);
                }
            }

            // 5. Lưu ĐỒNG LOẠT tất cả (Hóa đơn, Khách hàng, Bàn) xuống Database trong 1 Transaction
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
            // Lấy tất cả OrderDetail đang ở trạng thái Processing, include Product và Order(để lấy Table)
            var pendingItems = await _unitOfWork.Repository<OrderDetail>()
                .GetQueryable()
                .Include(od => od.Product)
                .Include(od => od.Order)
                    .ThenInclude(o => o.Table) // Kéo sang bảng Table để lấy tên Bàn
                .Where(od => od.Status == OrderItemStatus.Processing)
                .OrderBy(od => od.PrepStartTime) // Ai order trước làm trước (FIFO)
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

        // 2. Hàm khi Bếp bấm nút XONG
        public async Task MarkItemReadyAsync(Guid orderDetailId)
        {
            var orderDetail = await _unitOfWork.Repository<OrderDetail>().GetByIdAsync(orderDetailId);
            if (orderDetail == null) throw new Exception("Không tìm thấy món ăn này.");

            // Đổi trạng thái và chốt giờ hoàn thành
            orderDetail.Status = OrderItemStatus.Ready;
            orderDetail.PrepEndTime = DateTime.UtcNow;

            _unitOfWork.Repository<OrderDetail>().Update(orderDetail);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<OrderDto> OpenTableAsync(Guid tableId, Guid currentStaffId)
        {
            // 1. Kiểm tra xem bàn có tồn tại và đang trống không
            var table = await _unitOfWork.Repository<Table>().GetByIdAsync(tableId);
            if (table == null) throw new Exception("Không tìm thấy bàn này.");

            // Nếu bàn đã có khách rồi thì không cho mở đè lên
            if (table.Status == TableStatus.Occupied)
                throw new Exception("Bàn này đang có khách ngồi rồi, vui lòng chọn bàn khác!");

            // 2. TẠO MỘT HÓA ĐƠN RỖNG (Bill nháp chưa có món)
            var order = new Order
            {
                TableId = tableId,
                Status = OrderStatus.Processing, // Hóa đơn đang mở
                CreatedByStaffId = currentStaffId,
                OrderCode = $"ORD-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                TotalAmount = 0,
                FinalAmount = 0,
                PaymentMethod = "Cash" // Tạm để vậy
            };

            await _unitOfWork.Repository<Order>().AddAsync(order);

            // 3. ĐỔI TRẠNG THÁI BÀN SANG "CÓ KHÁCH" (Đỏ)
            table.Status = TableStatus.Occupied;
            _unitOfWork.Repository<Table>().Update(table);

            await _unitOfWork.SaveChangesAsync();

            // Trả về OrderId để Frontend lưu tạm vào biến state
            return new OrderDto
            {
                Id = order.Id,
                OrderCode = order.OrderCode,
                FinalAmount = order.FinalAmount,
                Status = order.Status.ToString()
            };
        }
    }
}