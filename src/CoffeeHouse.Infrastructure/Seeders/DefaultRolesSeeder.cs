using Microsoft.AspNetCore.Identity;

namespace CoffeeHouse.Infrastructure.Seeders
{
    public static class DefaultRolesSeeder
    {
        public static async Task SeedAsync(RoleManager<IdentityRole<Guid>> roleManager)
        {
            // Danh sách các quyền chuẩn của hệ thống
            var roles = new List<string> { "Admin", "Staff", "Kitchen", "Customer" };

            foreach (var roleName in roles)
            {
                // Kiểm tra xem quyền đã tồn tại trong Database chưa
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    // Nếu chưa, hệ thống tự động tạo mới cực kỳ chuẩn chỉ
                    await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                }
            }
        }
    }
}
