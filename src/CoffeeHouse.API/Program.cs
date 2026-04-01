using CoffeeHouse.API.Hubs;
using CoffeeHouse.Application.Exceptions;
using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Mappings;
using CoffeeHouse.Application.Services.Implementations;
using CoffeeHouse.Application.Services.Interfaces;
using CoffeeHouse.Domain.Entities;
using CoffeeHouse.Infrastructure;
using CoffeeHouse.Infrastructure.Repositories;
using CoffeeHouse.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace CoffeeHouse.API
{
    public class Program
    {
        // 1. ĐỔI THÀNH async Task Ở ĐÂY NÌ:
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // --- 1. CORE API SERVICES ---
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSignalR();

            // Cấu hình Swagger để hỗ trợ Token JWT
            builder.Services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Description = "Vui lòng nhập Token theo định dạng: Bearer {your_token}",
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    BearerFormat = "JWT",
                    Scheme = "Bearer"
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        Array.Empty<string>()
                     }
                 });
            });

            // --- 2. DATABASE & IDENTITY ---
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
            })
             .AddEntityFrameworkStores<ApplicationDbContext>()
             .AddDefaultTokenProviders();

            // --- 3. AUTHENTICATION & JWT ---
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:Key"]!)),
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["JWT:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["JWT:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

            // --- 4. CORS CONFIGURATION ---
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowVueApp", policy =>
                {
                    policy.WithOrigins("http://localhost:5173")
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            // --- 5. APPLICATION SERVICES (DI) ---
            builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);
            builder.Services.AddHttpContextAccessor();

            // Hệ thống Repository & Unit of Work
            builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Business Services
            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.AddScoped<IFileService, FileService>();
            builder.Services.AddScoped<IProductService, ProductService>();
            builder.Services.AddScoped<ICategoryService, CategoryService>();
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
            builder.Services.AddScoped<IAreaService, AreaService>();
            builder.Services.AddScoped<ITableService, TableService>();
            builder.Services.AddScoped<ICustomerService, CustomerService>();
            builder.Services.AddScoped<IStaffService, StaffService>();
            builder.Services.AddScoped<IOrderService, OrderService>();
            builder.Services.AddScoped<IReportService, ReportService>();
            builder.Services.AddScoped<IInvoiceService, InvoiceService>();
            builder.Services.AddScoped<IVoucherService, VoucherService>();


            // 2. KHỞI TẠO BIẾN app Ở ĐÂY
            var app = builder.Build();

            // 3. DỜI KHỐI SEED DATA XUỐNG ĐÂY (SAU KHI ĐÃ CÓ BIẾN app)
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    // Lấy RoleManager từ Service Provider
                    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                    await CoffeeHouse.Infrastructure.Seeders.DefaultRolesSeeder.SeedAsync(roleManager);

                    // 2. Lấy UserManager ra và Seed Tài khoản Admin
                    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                    await CoffeeHouse.Infrastructure.Seeders.DefaultUsersSeeder.SeedAsync(userManager);
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Có lỗi xảy ra trong quá trình Seed dữ liệu Roles.");
                }
            }

            // --- 6. MIDDLEWARE PIPELINE ---
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseStaticFiles();
            app.UseHttpsRedirection();
            app.UseCors("AllowVueApp");
            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    context.Response.ContentType = "application/json";

                    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;

                    var statusCode = error switch
                    {
                        BadRequestException => StatusCodes.Status400BadRequest,
                        NotFoundException => StatusCodes.Status404NotFound,
                        _ => StatusCodes.Status500InternalServerError
                    };

                    context.Response.StatusCode = statusCode;

                    await context.Response.WriteAsJsonAsync(new
                    {
                        statusCode,
                        message = error?.Message
                    });
                });
            });
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapHub<PaymentHub>("/paymentHub");
            app.Run();
        }
    }
}