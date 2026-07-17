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
using ApplicationLayer.Services.Promotions;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Chats;
using ApplicationLayer.Mappings;
using DomainLayer.Entities;
using DomainLayer.InterfaceCore.Email;
using DomainLayer.InterfaceCore.External;
using DomainLayer.InterfaceCore.JWT;
using DomainLayer.InterfaceRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using InfrastructureLayer.Cores.Notifications;
using InfrastructureLayer.Cores.AI;
using ApplicationLayer.AI.Services;

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
            services.AddScoped<IPromotionRepository, PromotionRepository>();
            services.AddScoped<IPromotionUsageRepository, PromotionUsageRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IConversationRepository, ConversationRepository>();
            services.AddScoped<IMessageRepository, MessageRepository>();
            services.AddScoped<IUserDeviceTokenRepository, UserDeviceTokenRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IFoodTagRepository, FoodTagRepository>();
            services.AddScoped<ICustomerPreferenceRepository, CustomerPreferenceRepository>();
            services.AddScoped<IAIRecommendationLogRepository, AIRecommendationLogRepository>();
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
            services.AddScoped<IPromotionService, PromotionService>();
            services.AddScoped<IPromotionValidationService, PromotionValidationService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IDeviceTokenService, DeviceTokenService>();
            services.AddScoped<IFoodTagService, FoodTagService>();
            services.AddScoped<ICustomerPreferenceService, CustomerPreferenceService>();
            services.AddScoped<IAIRecommendationService, AIRecommendationService>();
            services.AddSingleton<IOnlinePresenceService, OnlinePresenceService>();
            services.Configure<AIProviderSettings>(
                configuration.GetSection(AIProviderSettings.SectionName));
            services.AddHttpClient<IAIProviderService, GeminiAIProviderService>();
            services.Configure<FirebaseSettings>(
                configuration.GetSection(FirebaseSettings.SectionName));
            services.AddHttpClient<IPushNotificationService, FirebasePushNotificationService>();
            
            services.AddAutoMapper(_ => { }, typeof(MappingProfile).Assembly);

            // Add HttpContextAccessor
            services.AddHttpContextAccessor();


            return services;
        }
    }
}
