namespace CoffeeHouse.Application.DTOs.Staffs
{
    public class UpdateStaffDto
    {
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Role { get; set; } = "Staff";
    }
}
