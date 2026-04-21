using AutoMapper;
using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.DTOs.Products;
using CoffeeHouse.Application.Exceptions;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Domain.Entities;
using System.Linq.Expressions;

namespace CoffeeHouse.Application.Services.Implementations
{
    public class ProductService : IProductService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly IFileService _fileService;
        private readonly IInventoryService _inventoryService;

        public ProductService(IUnitOfWork uow, IMapper mapper, IFileService fileService, IInventoryService inventoryService)
        {
            _uow = uow;
            _mapper = mapper;
            _fileService = fileService;
            _inventoryService = inventoryService;
        }

        public async Task<PagedResult<ProductDto>> GetAllPagedAsync(ProductFilterDto filterDto)
        {
            var search = filterDto.Search?.Trim().ToLower();

            Expression<Func<Product, bool>> filter = p =>
                (string.IsNullOrEmpty(search) || p.Name.ToLower().Contains(search)) &&
                (!filterDto.CategoryId.HasValue || p.CategoryId == filterDto.CategoryId) &&
                (!filterDto.IsAvailable.HasValue || p.IsAvailable == filterDto.IsAvailable) &&
                (!filterDto.MinPrice.HasValue || p.Price >= filterDto.MinPrice) &&
                (!filterDto.MaxPrice.HasValue || p.Price <= filterDto.MaxPrice);

            var result = await _uow.Repository<Product>().GetAllPagedAsync(
                pageNumber: filterDto.PageNumber,
                pageSize: filterDto.PageSize,
                filter: filter,
                includeProperties: "Category",
                orderBy: q => q.OrderByDescending(p => p.CreatedAt)
            );

            var dtos = _mapper.Map<List<ProductDto>>(result.Items);

            // 👉 Tính MaxAvailableServings cho batch sản phẩm (1 query DB duy nhất)
            var productIds = dtos.Select(d => d.Id).ToList();
            var servingsMap = await _inventoryService.CalculateMaxServingsForProductsAsync(productIds);

            foreach (var dto in dtos)
            {
                if (servingsMap.TryGetValue(dto.Id, out int maxServings))
                {
                    // int.MaxValue = không có recipe → frontend nhận -1 (không giới hạn)
                    dto.MaxAvailableServings = maxServings == int.MaxValue ? -1 : maxServings;
                    dto.IsOutOfStock = maxServings <= 0;
                }
                else
                {
                    dto.MaxAvailableServings = -1;
                    dto.IsOutOfStock = false;
                }
            }

            return new PagedResult<ProductDto>
            {
                Items = dtos,
                TotalCount = result.TotalCount,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize
            };
        }

        public async Task<ProductDto?> GetByIdAsync(Guid id)
        {
            var product = await _uow.Repository<Product>().GetByIdAsync(id);
            return _mapper.Map<ProductDto>(product);
        }

        public async Task<ProductDto> CreateAsync(CreateUpdateProductDto dto)
        {
            var product = _mapper.Map<Product>(dto);

            if (dto.ImageFile != null)
            {
                product.ImageUrl = await _fileService.SaveFileAsync(dto.ImageFile, "products");
            }
            await _uow.Repository<Product>().AddAsync(product);
            await _uow.SaveChangesAsync();

            return _mapper.Map<ProductDto>(product);
        }

        public async Task UpdateAsync(Guid id, CreateUpdateProductDto dto)
        {
            var product = await _uow.Repository<Product>().GetByIdAsync(id);
            if (product == null) throw new NotFoundException("Sản phẩm không tồn tại.");

            _mapper.Map(dto, product);

            if (dto.ImageFile != null)
            {
                _fileService.DeleteFile(product.ImageUrl);
                product.ImageUrl = await _fileService.SaveFileAsync(dto.ImageFile, "products");
            }

            product.UpdatedAt = DateTime.UtcNow;
            _uow.Repository<Product>().Update(product);
            await _uow.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var product = await _uow.Repository<Product>().GetByIdAsync(id);
            if (product == null) throw new NotFoundException("Sản phẩm không tồn tại.");

            _fileService.DeleteFile(product.ImageUrl);
            _uow.Repository<Product>().Delete(product);
            await _uow.SaveChangesAsync();
        }
    }
}