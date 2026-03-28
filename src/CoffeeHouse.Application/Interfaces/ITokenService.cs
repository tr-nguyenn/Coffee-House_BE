using CoffeeHouse.Infrastructure;

namespace CoffeeHouse.Application.Interfaces
{
    public interface ITokenService
    {
        string CreateToken(ApplicationUser user, IList<string> roles);
    }
}
