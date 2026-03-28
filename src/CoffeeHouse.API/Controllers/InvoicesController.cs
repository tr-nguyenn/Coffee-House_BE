using CoffeeHouse.Application.DTOs.Invoices;
using CoffeeHouse.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeHouse.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Staff")]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;

        public InvoicesController(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        [HttpGet]
        public async Task<IActionResult> GetInvoices([FromQuery] InvoiceFilterDto filter)
        {
            var result = await _invoiceService.GetInvoicesPagedAsync(filter);
            return Ok(new { data = result });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetInvoiceDetail(Guid id)
        {
            try
            {
                var result = await _invoiceService.GetInvoiceDetailAsync(id);
                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}
