using CoffeeHouse.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

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
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<Material> Materials { get; set; }
        public DbSet<ProductRecipe> ProductRecipes { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 2. Đổi tên các bảng Identity (PHẢI DÙNG <Guid>)
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

            // 3.1 Category Configuration
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

            // 3.6 Cấu hình Voucher (Mã giảm giá)
            builder.Entity<Voucher>(entity => {
                entity.HasIndex(v => v.Code).IsUnique();
                entity.Property(v => v.Code).IsRequired().HasMaxLength(50);
                entity.Property(v => v.DiscountValue).HasColumnType("decimal(18,2)");
                entity.Property(v => v.MaxDiscountAmount).HasColumnType("decimal(18,2)");
                entity.Property(v => v.MinOrderAmount).HasColumnType("decimal(18,2)");
            });

            // CẤU HÌNH MATERIAL (VẬT TƯ)
            builder.Entity<Material>(entity =>
            {
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Unit).IsRequired().HasMaxLength(50);
                // Định dạng số thập phân: 18 chữ số, 3 số sau dấu phẩy (Ví dụ: 1.500 kg)
                entity.Property(e => e.StockQuantity).HasColumnType("decimal(18, 3)");
                entity.Property(e => e.MinStockLevel).HasColumnType("decimal(18, 3)");
                entity.Property(e => e.CostPerUnit).HasColumnType("decimal(18, 2)");
            });

            // CẤU HÌNH PRODUCT RECIPE (CÔNG THỨC)
            builder.Entity<ProductRecipe>(entity =>
            {
                entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");

                // 1 Sản phẩm và 1 Vật tư chỉ được xuất hiện cùng nhau 1 lần (Tránh lỗi add trùng bột cafe 2 lần cho 1 ly cafe)
                entity.HasIndex(e => new { e.ProductId, e.MaterialId }).IsUnique();

                entity.HasOne(d => d.Product)
                    .WithMany(p => p.ProductRecipes)
                    .HasForeignKey(d => d.ProductId)
                    .OnDelete(DeleteBehavior.Cascade); // Xóa Món thì xóa luôn công thức

                entity.HasOne(d => d.Material)
                    .WithMany(p => p.ProductRecipes)
                    .HasForeignKey(d => d.MaterialId)
                    .OnDelete(DeleteBehavior.Restrict); // CẤM xóa Vật tư nếu đang có món xài nó
            });

            // CẤU HÌNH INVENTORY TRANSACTION (LỊCH SỬ KHO)
            builder.Entity<InventoryTransaction>(entity =>
            {
                entity.Property(e => e.QuantityChanged).HasColumnType("decimal(18, 3)");
                entity.Property(e => e.RemainingQuantity).HasColumnType("decimal(18, 3)");
                entity.Property(e => e.Note).HasMaxLength(500);

                entity.HasOne(d => d.Material)
                    .WithMany(p => p.InventoryTransactions)
                    .HasForeignKey(d => d.MaterialId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}