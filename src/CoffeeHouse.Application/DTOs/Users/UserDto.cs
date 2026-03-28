namespace CoffeeHouse.Application.DTOs.Users
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public int RewardPoints { get; set; }
    }
}
