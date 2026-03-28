using CoffeeHouse.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace CoffeeHouse.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // Lấy ID từ Claim "NameIdentifier" đã được lưu khi tạo Token
        public Guid? UserId
        {
            get
            {
                var id = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                return string.IsNullOrEmpty(id) ? null : Guid.Parse(id);
            }
        }

        public string? UserName => _httpContextAccessor.HttpContext?.User?.Identity?.Name;
    }
}
