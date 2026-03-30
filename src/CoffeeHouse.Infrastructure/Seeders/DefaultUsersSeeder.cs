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
                var newAdmin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Quản trị viên tối cao",
                    PhoneNumber = "0999999999",
                    EmailConfirmed = true 
                };

                var result = await userManager.CreateAsync(newAdmin, "Admin@123");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(newAdmin, "Admin");
                }
            }
        }
    }
}
