using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Orders;
using CoffeeHouse.Domain.Entities;

namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface IOrderService
    {
        Task<OrderDto> CreateOrderAsync(CreateOrderDto dto, Guid currentStaffId);
        Task<OrderDto> GetOrderByIdAsync(Guid orderId);
        Task<OrderDto> AddItemsToOrderAsync(Guid orderId, AddOrderItemsDto dto);
        Task<OrderDto> CheckoutOrderAsync(Guid orderId, CheckoutOrderDto dto);
        Task<List<KitchenTicketDto>> GetPendingKitchenItemsAsync();
        Task MarkItemReadyAsync(Guid orderDetailId);
        Task<OrderDto> OpenTableAsync(Guid tableId, Guid currentStaffId);
        Task UpdatePaymentMethodAsync(Guid orderId, string paymentMethod);
        Task<PagedResult<OrderManagementDto>> GetManagementOrdersAsync(OrderFilterDto filter);
        Task<byte[]> ExportManagementOrdersToExcelAsync(OrderFilterDto filter);
    }
}
