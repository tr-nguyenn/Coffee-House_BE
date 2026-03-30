using AutoMapper;
using CoffeeHouse.Application.DTOs.Area;
using CoffeeHouse.Application.DTOs.Categories;
using CoffeeHouse.Application.DTOs.Products;
using CoffeeHouse.Application.DTOs.Staffs;
using CoffeeHouse.Application.DTOs.Tables;
using CoffeeHouse.Application.DTOs.Customer;
using CoffeeHouse.Domain.Entities;
using CoffeeHouse.Infrastructure;

namespace CoffeeHouse.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // 1. Map từ Entity sang DTO để hiển thị (Dùng cho Get/GetAll)
            CreateMap<Product, ProductDto>()
                // Dùng ?. để tránh lỗi NullReferenceException nếu chẳng may Category bị null
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : "N/A"));

            // 2. Map từ DTO sang Entity để lưu vào DB (Dùng cho Create/Update)
            // QUAN TRỌNG: Phải dùng CreateUpdateProductDto (đúng tên bạn dùng ở Service)
            CreateMap<CreateUpdateProductDto, Product>()
                // Bảo AutoMapper: "Này, đừng đụng vào ImageUrl, tao sẽ tự xử lý nó bằng FileService"
                .ForMember(dest => dest.ImageUrl, opt => opt.Ignore());

            // 3. Mapping cho Category (Giữ nguyên vì đã đúng)
            CreateMap<Category, CategoryDto>();
            CreateMap<CreateUpdateCategoryDto, Category>();

            CreateMap<Area, AreaDto>().ReverseMap();
            CreateMap<CreateAreaDto, Area>();
            CreateMap<UpdateAreaDto, Area>();

            CreateMap<Table, TableDto>()
                .ForMember(dest => dest.AreaName, opt => opt.MapFrom(src => src.Area != null ? src.Area.Name : "N/A"));
            CreateMap<CreateTableDto, Table>();
            CreateMap<UpdateTableDto, Table>();

            CreateMap<Customer, UserDto>().ReverseMap();
            CreateMap<CreateUserDto, Customer>();
            CreateMap<UpdateUserDto, Customer>();

            CreateMap<ApplicationUser, StaffDto>()
                .ForMember(dest => dest.Roles, opt => opt.Ignore());
        }
    }
}