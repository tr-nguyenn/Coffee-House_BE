using CoffeeHouse.Application.Common;

namespace CoffeeHouse.Application.DTOs.Vouchers
{
    public class VoucherFilterDto : BaseFilterDto
    {
        public bool? ValidOnly { get; set; }
    }
}
