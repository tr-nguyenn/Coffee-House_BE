using CoffeeHouse.Application.Common;

namespace CoffeeHouse.Application.DTOs.Products
{
    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsAvailable { get; set; }
        public Guid CategoryId { get; set; }
        public string? CategoryName { get; set; }

        /// <summary>Số lượng ly tối đa có thể pha dựa trên tồn kho. -1 = không có công thức (không giới hạn).</summary>
        public int MaxAvailableServings { get; set; } = -1;
        /// <summary>True nếu MaxAvailableServings == 0 (hết nguyên liệu).</summary>
        public bool IsOutOfStock { get; set; }
    }

    public class ProductFilterDto : BaseFilterDto
    {
        public Guid? CategoryId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public bool? IsAvailable { get; set; } // Thêm trạng thái
    }

    public class CreateUpdateProductDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public Guid CategoryId { get; set; }
        public Microsoft.AspNetCore.Http.IFormFile? ImageFile { get; set; }
        public bool IsAvailable { get; set; } = true;
    }
}
