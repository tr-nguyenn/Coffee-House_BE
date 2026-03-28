namespace CoffeeHouse.Application.DTOs.Users
{
    public class CustomerCompactDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public int Points { get; set; } 
    }
}
