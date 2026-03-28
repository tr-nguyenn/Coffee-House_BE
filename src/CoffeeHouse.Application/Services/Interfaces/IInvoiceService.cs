using CoffeeHouse.Application.DTOs.Invoices;

namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface IInvoiceService
    {
        Task<InvoicePagedResult> GetInvoicesPagedAsync(InvoiceFilterDto filter);
        Task<InvoiceDetailDto> GetInvoiceDetailAsync(Guid orderId);
    }
}
