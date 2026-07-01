using InfrastructureLayer.Data;
using InfrastructureLayer.Cores.Emails;
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
using ApplicationLayer.Services.Zones;
using ApplicationLayer.Services.MarketLayouts;
using ApplicationLayer.Services.LayoutNodes;
using ApplicationLayer.Services.LayoutEdges;
using ApplicationLayer.Services.BoothLocations;
using ApplicationLayer.Services.MapNavigation;
using ApplicationLayer.Services.Packages;
using ApplicationLayer.Services.Prices;
using ApplicationLayer.Services.Carts;
using ApplicationLayer.Mappings;
using DomainLayer.Entities;
using DomainLayer.InterfaceCore.Email;
using DomainLayer.InterfaceCore.External;
using DomainLayer.InterfaceCore.JWT;
using DomainLayer.InterfaceRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            services.AddScoped<IBoothRegistrationRepository, BoothRegistrationRepository>();
            services.AddScoped<IFoodCategoryRepository, FoodCategoryRepository>();
            services.AddScoped<IFoodItemRepository, FoodItemRepository>();
            services.AddScoped<IFoodPriceRepository, FoodPriceRepository>();
            services.AddScoped<IPackagePriceRepository, PackagePriceRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IComplaintRepository, ComplaintRepository>();
            services.AddScoped<IReviewRepository, ReviewRepository>();
            services.AddScoped<INightMarketRepository, NightMarketRepository>();
            services.AddScoped<IZoneRepository, ZoneRepository>();
            services.AddScoped<IMarketLayoutRepository, MarketLayoutRepository>();
            services.AddScoped<ILayoutNodeRepository, LayoutNodeRepository>();
            services.AddScoped<ILayoutEdgeRepository, LayoutEdgeRepository>();
            services.AddScoped<IBoothLocationRepository, BoothLocationRepository>();
            services.AddScoped<ICartRepository, CartRepository>();
            services.AddScoped<ICartItemRepository, CartItemRepository>();
            services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
            services.AddScoped<IJwtService, JWTService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IEmailService, EmailService>();
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
            services.AddScoped<IZoneService, ZoneService>();
            services.AddScoped<IMarketLayoutService, MarketLayoutService>();
            services.AddScoped<ILayoutGraphValidationService, LayoutGraphValidationService>();
            services.AddScoped<ILayoutNodeService, LayoutNodeService>();
            services.AddScoped<ILayoutEdgeService, LayoutEdgeService>();
            services.AddScoped<IBoothLocationService, BoothLocationService>();
            services.AddScoped<IMapNavigationService, MapNavigationService>();
            services.AddScoped<IPackageService, PackageService>();
            services.AddScoped<IPriceService, PriceService>();
            services.AddScoped<ICartService, CartService>();
            
            services.AddAutoMapper(_ => { }, typeof(MappingProfile).Assembly);

            // Add HttpContextAccessor
            services.AddHttpContextAccessor();


            return services;
        }
    }
}
