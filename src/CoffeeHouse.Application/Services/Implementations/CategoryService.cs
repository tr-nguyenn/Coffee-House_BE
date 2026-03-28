using AutoMapper;
using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Categories;
using CoffeeHouse.Application.Exceptions;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Domain.Entities;
using System.Linq.Expressions;

namespace CoffeeHouse.Application.Services.Implementations
{
    public class CategoryService : ICategoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CategoryService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<PagedResult<CategoryDto>> GetAllPagedAsync(CategoryFilterDto filterDto)
        {
            var searchTerm = filterDto.Search?.Trim().ToLower();
            Expression<Func<Category, bool>>? filter = null;
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                filter = c => c.Name.ToLower().Contains(searchTerm) ||
                              (c.Description != null && c.Description.ToLower().Contains(searchTerm));
            }

            var result = await _unitOfWork.Repository<Category>().GetAllPagedAsync(
                pageNumber: filterDto.PageNumber,
                pageSize: filterDto.PageSize,
                filter: filter,
                orderBy: q => q.OrderBy(c => c.CreatedAt)
            );

            return new PagedResult<CategoryDto>
            {
                Items = _mapper.Map<List<CategoryDto>>(result.Items),
                TotalCount = result.TotalCount,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize
            };
        }

        public async Task<CategoryDto?> GetByIdAsync(Guid id)
        {
            var category = await _unitOfWork.Repository<Category>().GetByIdAsync(id);
            return _mapper.Map<CategoryDto>(category);
        }

        public async Task<CategoryDto> CreateAsync(CreateUpdateCategoryDto dto)
        {
            // Kiểm tra trùng tên chuyên nghiệp
            var existing = await _unitOfWork.Repository<Category>().GetAllPagedAsync(filter: c => c.Name == dto.Name);
            if (existing.TotalCount > 0) throw new NotFoundException("Tên loại đồ uống này đã tồn tại.");

            var category = _mapper.Map<Category>(dto);
            await _unitOfWork.Repository<Category>().AddAsync(category);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<CategoryDto>(category);
        }

        public async Task UpdateAsync(Guid id, CreateUpdateCategoryDto dto)
        {
            var category = await _unitOfWork.Repository<Category>().GetByIdAsync(id);
            if (category == null) throw new Exception("Không tìm thấy loại đồ uống.");

            _mapper.Map(dto, category);
            category.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<Category>().Update(category);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            // Kiểm tra xem có sản phẩm nào đang thuộc loại này không (Ràng buộc toàn vẹn)
            var categoryWithProducts = await _unitOfWork.Repository<Category>().GetAllPagedAsync(
                filter: c => c.Id == id,
                includeProperties: "Products");

            var target = categoryWithProducts.Items.FirstOrDefault();
            if (target == null) throw new Exception("Không tìm thấy loại đồ uống.");

            if (target.Products != null && target.Products.Any())
                throw new Exception("Không thể xóa vì đang có sản phẩm thuộc loại này.");

            _unitOfWork.Repository<Category>().Delete(target);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
