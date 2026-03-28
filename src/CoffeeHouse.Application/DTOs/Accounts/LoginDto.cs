namespace CoffeeHouse.Application.DTOs.Accounts
{
    public class LoginDto
    {
        public string UserNameOrEmail { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
