using Microsoft.AspNetCore.Identity;

namespace CoffeeHouse.Infrastructure.Seeders
{
    public static class DefaultUsersSeeder
    {
        public static async Task SeedAsync(UserManager<ApplicationUser> userManager)
        {
            var adminEmail = "admin@gmail.com";
            var existingAdmin = await userManager.FindByEmailAsync(adminEmail);


            if (existingAdmin == null)
            {
                // Nếu chưa có thì khởi tạo thông tin
                var newAdmin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Quản trị viên tối cao",
                    PhoneNumber = "0999999999",
                    EmailConfirmed = true // Gán true luôn để khỏi bắt xác thực email lằng nhằng
                };

                var result = await userManager.CreateAsync(newAdmin, "Admin@123");

                if (result.Succeeded)
                {
                    // Tạo thành công thì khoác ngay cái áo bào "Admin" cho tài khoản ni
                    await userManager.AddToRoleAsync(newAdmin, "Admin");
                }
            }
        }
    }
}
