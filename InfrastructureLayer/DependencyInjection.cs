using InfrastructureLayer.Data;
using InfrastructureLayer.Cores.Emails;
using InfrastructureLayer.Cores.Auth;
using InfrastructureLayer.Cores.External;
using InfrastructureLayer.Cores.Helppers;
using InfrastructureLayer.Cores.JWTs;
using InfrastructureLayer.Repositories;
using ApplicationLayer.Services.Auth;
using ApplicationLayer.Services.Account;
using ApplicationLayer.Services.BoothRegistrations;
using ApplicationLayer.Services.Booths;
using ApplicationLayer.Services.FoodCategories;
using ApplicationLayer.Services.Menus;
using ApplicationLayer.Services.NightMarkets;
using ApplicationLayer.Services.Complaints;
using ApplicationLayer.Services.Reviews;
using ApplicationLayer.Mappings;
using DomainLayer.Entities;
using DomainLayer.InterfaceCore.Email;
using DomainLayer.InterfaceCore.Auth;
using DomainLayer.InterfaceCore.External;
using DomainLayer.InterfaceCore.JWT;
using DomainLayer.InterfaceRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace InfrastructureLayer
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Add DbContext
            services.AddDbContext<SNMDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly(typeof(SNMDbContext).Assembly.FullName)
                )
            );

            // Register Repositories
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IBoothRepository, BoothRepository>();
            services.AddScoped<IFoodCategoryRepository, FoodCategoryRepository>();
            services.AddScoped<IFoodItemRepository, FoodItemRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IComplaintRepository, ComplaintRepository>();
            services.AddScoped<IReviewRepository, ReviewRepository>();
            services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
            services.AddScoped<IJwtService, JWTService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IEmailService, EmailService>();
            
            var redisConnectionString = configuration["Redis:ConnectionString"];
            if (string.IsNullOrWhiteSpace(redisConnectionString))
                throw new InvalidOperationException("Redis:ConnectionString must be configured.");
            
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
            services.AddScoped<IAuthTokenStore, RedisAuthTokenStore>();
            services.AddHttpClient<IGoogleTokenValidator, GoogleTokenValidator>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IAccountService, AccountService>();
            
            services.AddScoped<IBoothRegistrationService, BoothRegistrationService>();
            services.AddScoped<IBoothService, BoothService>();
            
            services.AddScoped<IFoodCategoryService, FoodCategoryService>();
            services.AddScoped<IMenuService, MenuService>();
            
            services.AddScoped<INightMarketService, NightMarketService>();
            services.AddScoped<IComplaintService, ComplaintService>();
            services.AddScoped<IReviewService, ReviewService>();
            
            services.AddAutoMapper(typeof(MappingProfile).Assembly);

            // Add HttpContextAccessor
            services.AddHttpContextAccessor();


            return services;
        }
    }
}
