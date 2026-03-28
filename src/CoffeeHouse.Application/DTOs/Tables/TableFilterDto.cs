using CoffeeHouse.Application.Common;
using CoffeeHouse.Domain.Enums;

namespace CoffeeHouse.Application.DTOs.Tables
{
    public class TableFilterDto : BaseFilterDto
    {
        public Guid? AreaId { get; set; }
        public TableStatus? Status { get; set; }
    }
}
