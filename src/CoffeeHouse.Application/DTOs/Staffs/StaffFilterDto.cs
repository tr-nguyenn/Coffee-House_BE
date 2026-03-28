using CoffeeHouse.Application.Common;

namespace CoffeeHouse.Application.DTOs.Staffs
{
    public class StaffFilterDto : BaseFilterDto
    {
        public string? Role { get; set; }
    }
}
