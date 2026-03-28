using Microsoft.AspNetCore.Identity;

namespace CoffeeHouse.Infrastructure
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string FullName { get; set; } = string.Empty;

    }
}
