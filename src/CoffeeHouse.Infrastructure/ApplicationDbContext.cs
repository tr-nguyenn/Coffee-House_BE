using CoffeeHouse.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CoffeeHouse.Infrastructure
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // 1. Khai báo các bảng (DbSet) từ tầng Domain
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Staff> Staffs { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Area> Areas { get; set; }
        public DbSet<Table> Tables { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 2. Đổi tên các bảng Identity (PHẢI DÙNG <Guid>)
            // Nếu bạn dùng <string> ở đây, code sẽ báo lỗi ngay lập tức
            builder.Entity<ApplicationUser>().ToTable("AppUsers");
            builder.Entity<IdentityRole<Guid>>().ToTable("AppRoles");
            builder.Entity<IdentityUserRole<Guid>>().ToTable("AppUserRoles");
            builder.Entity<IdentityUserClaim<Guid>>().ToTable("AppUserClaims");
            builder.Entity<IdentityUserLogin<Guid>>().ToTable("AppUserLogins");
            builder.Entity<IdentityRoleClaim<Guid>>().ToTable("AppRoleClaims");
            builder.Entity<IdentityUserToken<Guid>>().ToTable("AppUserTokens");

            // 3. Cấu hình Fluent API cho các bảng Domain
            builder.Entity<Customer>(entity =>
            {
                entity.HasIndex(e => e.IdentityId).IsUnique();
            });

            // 3.1 Category Configuration (Bạn có thể viết trực tiếp hoặc tách file)
            builder.Entity<Category>(entity => {
                entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            });

            // 3.2 Product Configuration
            builder.Entity<Product>(entity => {
                entity.Property(p => p.Price).HasColumnType("decimal(18,2)");
                // Thiết lập quan hệ 1-N (Một Category có nhiều Product)
                entity.HasOne(p => p.Category)
                      .WithMany(c => c.Products)
                      .HasForeignKey(p => p.CategoryId);
            });

            // 3.4 Cấu hình Area (Khu vực)
            builder.Entity<Area>(entity => {
                entity.Property(a => a.Name).IsRequired().HasMaxLength(100);
            });

            // 3.5 Cấu hình Table (Bàn)
            builder.Entity<Table>(entity => {
                entity.Property(t => t.Name).IsRequired().HasMaxLength(50);

                // Mối quan hệ: 1 Bàn thuộc 1 Khu vực
                entity.HasOne(t => t.Area)
                      .WithMany(a => a.Tables)
                      .HasForeignKey(t => t.AreaId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            //3.3
            builder.Entity<Order>(entity => {
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.FinalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");

                // Cấu hình quan hệ với nhân viên tạo đơn
                entity.HasOne(e => e.CreatedByStaff)
                      .WithMany()
                      .HasForeignKey(e => e.CreatedByStaffId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<OrderDetail>(entity => {
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
            });
        }
    }
}