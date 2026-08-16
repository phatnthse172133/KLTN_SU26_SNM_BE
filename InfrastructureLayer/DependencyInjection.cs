using ApplicationLayer.Configuration;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.Account;
using ApplicationLayer.Services.AdminModeration;
using ApplicationLayer.Services.Auth;
using ApplicationLayer.Services.BoothDashboard;
using ApplicationLayer.Services.BoothLocations;
using ApplicationLayer.Services.BoothMedia;
using ApplicationLayer.Services.BoothPayOsCredentials;
//using ApplicationLayer.Services.BoothRegistrations;
using ApplicationLayer.Services.Booths;
using ApplicationLayer.Services.Carts;
using ApplicationLayer.Services.Chats;
using ApplicationLayer.Services.Complaints;
using ApplicationLayer.Services.CustomerDiscovery;
using ApplicationLayer.Services.Assistant;
using ApplicationLayer.Services.CustomerFoodProfiles;
using InfrastructureLayer.Cores.Assistant;
using ApplicationLayer.Services.Dashboard;
using ApplicationLayer.Services.EncryptionServices;
using ApplicationLayer.Services.FoodCategories;
using ApplicationLayer.Services.FoodTags;
using ApplicationLayer.Services.IndoorPositioning;
using ApplicationLayer.Services.LayoutEdges;
using ApplicationLayer.Services.LayoutNodes;
using ApplicationLayer.Services.MapNavigation;
using ApplicationLayer.Services.MarketLayouts;
using ApplicationLayer.Services.MarketOwnerDashboard;
using ApplicationLayer.Services.Menus;
using ApplicationLayer.Services.NavigationAnchors;
using ApplicationLayer.Services.NightMarkets;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Orders;
using ApplicationLayer.Services.Packages;
using ApplicationLayer.Services.PaymentMethods;
using ApplicationLayer.Services.PayOS;
using ApplicationLayer.Services.PayOutClients;
using ApplicationLayer.Services.Prices;
using ApplicationLayer.Services.Promotions;
using ApplicationLayer.Services.Reviews;
using ApplicationLayer.Services.Storage;
using ApplicationLayer.Services.Subscriptions;
using ApplicationLayer.Services.Support;
using ApplicationLayer.Services.Zones;
using DomainLayer.Entities;
using DomainLayer.InterfaceCore.Email;
using DomainLayer.InterfaceCore.External;
using DomainLayer.InterfaceCore.JWT;
using DomainLayer.InterfaceRepositories;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Backgrounds;
using InfrastructureLayer.Cores.Emails;
using InfrastructureLayer.Cores.External;
using InfrastructureLayer.Cores.Helppers;
using InfrastructureLayer.Cores.JWTs;
using InfrastructureLayer.Cores.Notifications;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using InfrastructureLayer.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IBoothRepository, BoothRepository>();
            services.AddScoped<IFoodCategoryRepository, FoodCategoryRepository>();
            services.AddScoped<IFoodItemRepository, FoodItemRepository>();
            services.AddScoped<IFoodSemanticMetadataRepository, FoodSemanticMetadataRepository>();
            services.AddScoped<IFoodPriceRepository, FoodPriceRepository>();
            services.AddScoped<IPackagePriceRepository, PackagePriceRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IComplaintRepository, ComplaintRepository>();
            services.AddScoped<IReviewRepository, ReviewRepository>();
            services.AddScoped<IFoodReviewRepository, FoodReviewRepository>();
            services.AddScoped<INightMarketRepository, NightMarketRepository>();
            services.AddScoped<INightMarketImageRepository, NightMarketImageRepository>();
            services.AddScoped<IBoothImageRepository, BoothImageRepository>();
            services.AddScoped<IZoneRepository, ZoneRepository>();
            services.AddScoped<IMarketLayoutRepository, MarketLayoutRepository>();
            services.AddScoped<ILayoutNodeRepository, LayoutNodeRepository>();
            services.AddScoped<ILayoutEdgeRepository, LayoutEdgeRepository>();
            services.AddScoped<IBoothLocationRepository, BoothLocationRepository>();
            services.AddScoped<ILayoutNavigationAnchorRepository, LayoutNavigationAnchorRepository>();
            services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
            services.AddScoped<IDashboardRepository, DashboardRepository>();
            services.AddScoped<ICartRepository, CartRepository>();
            services.AddScoped<ICartItemRepository, CartItemRepository>();
            services.AddScoped<IPromotionRepository, PromotionRepository>();
            services.AddScoped<IPromotionUsageRepository, PromotionUsageRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IConversationRepository, ConversationRepository>();
            services.AddScoped<ISupportTicketRepository, SupportTicketRepository>();
            services.AddScoped<IMessageRepository, MessageRepository>();
            services.AddScoped<IMarketOwnerDashboardRepository, MarketOwnerDashboardRepository>();
            services.AddScoped<IUserDeviceTokenRepository, UserDeviceTokenRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
            services.AddScoped<IFoodTagRepository, FoodTagRepository>();
            services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
            services.AddScoped<IJwtService, JWTService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IAccountService, AccountService>();

            services.AddScoped<IBoothService, BoothService>();

            services.AddScoped<IBoothPayOsCredentialRepository, BoothPayOsCredentialRepository>();
            services.AddScoped<IBoothPayOsCredentialService, BoothPayOsCredentialService>();
            services.AddScoped<IEncryptionService, AesEncryptionService>();
            services.AddScoped<IPayOSPayoutClientFactory, PayOSPayoutClientFactory>();

            services.AddScoped<IPayOSService, PayOSService>(sp =>
                ActivatorUtilities.CreateInstance<PayOSService>(sp, false));
            services.AddKeyedScoped<IPayOSService, PayOSService>("SubscriptionPayOS", (sp, key) =>
                ActivatorUtilities.CreateInstance<PayOSService>(sp, true));

            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<ICustomerCheckoutService, CustomerCheckoutService>();
            services.AddScoped<BoothOperatingHoursEvaluator>();

            services.AddScoped<IFoodCategoryService, FoodCategoryService>();
            services.AddScoped<IMenuService, MenuService>();
            services.AddScoped<IFoodMetadataCatalogService, FoodMetadataCatalogService>();
            services.AddScoped<ILegacyFoodTagMetadataAdapter, LegacyFoodTagMetadataAdapter>();

            services.AddScoped<INightMarketService, NightMarketService>();
            services.AddScoped<ICustomerDiscoveryService, CustomerDiscoveryService>();
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<IComplaintService, ComplaintService>();
            services.AddScoped<IReviewService, ReviewService>();
            services.AddScoped<IZoneService, ZoneService>();
            services.AddScoped<IMarketLayoutService, MarketLayoutService>();
            services.AddScoped<ILayoutGeneratorService, LayoutGeneratorService>();
            services.AddScoped<ILayoutGraphValidationService, LayoutGraphValidationService>();
            services.AddScoped<ILayoutNodeService, LayoutNodeService>();
            services.AddScoped<ILayoutEdgeService, LayoutEdgeService>();
            services.AddScoped<IBoothLocationService, BoothLocationService>();
            services.AddScoped<IMapNavigationService, MapNavigationService>();
            services.AddScoped<IIndoorRouteSolver, DijkstraIndoorRouteSolver>();
            services.AddScoped<IIndoorRouteInstructionBuilder, IndoorRouteInstructionBuilder>();
            services.AddScoped<INavigationGraphBuilder, NavigationGraphBuilder>();
            services.AddScoped<INavigationAnchorService, NavigationAnchorService>();
            services.AddScoped<IIndoorPositioningService, IndoorPositioningService>();
            services.AddScoped<IIndoorPositioningService, IndoorPositioningService>();
            services.Configure<IndoorNavigationOptions>(configuration.GetSection(IndoorNavigationOptions.SectionName));
            services.AddScoped<IPackageService, PackageService>();
            services.AddScoped<ISubscriptionService, SubscriptionService>();
            services.AddScoped<IOwnerSubscriptionService, OwnerSubscriptionService>();
            services.AddScoped<IPackagePolicyService, PackagePolicyService>();
            services.AddScoped<IPayOSWebhookService, PayOSWebhookService>();
            services.AddScoped<ISubscriptionEntitlementService, SubscriptionEntitlementService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IMarketOwnerDashboardService, MarketOwnerDashboardService>();
            services.AddScoped<IBoothDashboardService, BoothDashboardService>();
            services.AddScoped<IBoothAnalyticsService, BoothAnalyticsService>();
            services.AddScoped<IBoothDashboardRepository, BoothDashboardRepository>();
            services.AddScoped<IBoothMediaService, BoothMediaService>();
            services.AddScoped<IImageUploadService, ImageUploadService>();
            services.AddScoped<IPriceService, PriceService>();
            services.AddScoped<ICartService, CartService>();
            services.AddScoped<IPromotionService, PromotionService>();
            services.AddScoped<IPromotionValidationService, PromotionValidationService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<ISupportTicketService, SupportTicketService>();
            services.AddScoped<IDeviceTokenService, DeviceTokenService>();
            services.AddScoped<IFoodTagService, FoodTagService>();
            services.AddScoped<ICustomerFoodProfileService, CustomerFoodProfileService>();
            services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
            services.Configure<AssistantOptions>(configuration.GetSection(AssistantOptions.SectionName));
            services.AddHttpClient<ILanguageModelClient, OpenAiLanguageModelClient>((sp, client) =>
            {
                var settings = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
                client.Timeout = Timeout.InfiniteTimeSpan;
                var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? "https://api.openai.com/v1/" : settings.BaseUrl.TrimEnd('/') + "/";
                if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
                    client.BaseAddress = uri;
            });
            services.AddScoped<IAssistantConversationRepository, AssistantConversationRepository>();
            services.AddScoped<IAssistantFoodQueryRepository, AssistantFoodQueryRepository>();
            services.AddScoped<AssistantIntentInterpreter>();
            services.AddScoped<AssistantSemanticMatcher>();
            services.AddScoped<AssistantCompatibilityScorer>();
            services.AddScoped<AssistantMealPlanValidator>();
            services.AddScoped<AssistantMealPlanComposer>();
            services.AddScoped<AssistantReplyComposer>();
            services.AddScoped<IAssistantService, AssistantService>();
            services.AddSingleton<IOnlinePresenceService, OnlinePresenceService>();
            services.Configure<FirebaseSettings>(
                configuration.GetSection(FirebaseSettings.SectionName));
            services.AddHttpClient<IPushNotificationService, FirebasePushNotificationService>();
            services.AddScoped<IPaymentMethodService, PaymentMethodService>();

            // PayOS configuration and service (uses PayOSClient singleton registered in Program.cs)
            services.Configure<PayOSSettings>(configuration.GetSection(PayOSSettings.SectionName));
            services.AddScoped<IPayOSPayoutService, PayOSPayoutService>();
            services.AddScoped<DomainLayer.InterfaceRepository.ISequenceRepository, SequenceRepository>();
            services.AddScoped<IPayOSOrderCodeGenerator, PayOSOrderCodeGenerator>();
            services.AddScoped<IPayOSWebhookDispatcher, PayOSWebhookDispatcher>();

            services.AddScoped<IModerationRepository, ModerationRepository>();
            services.AddScoped<IAdminModerationService, AdminModerationService>();

            services.AddAutoMapper(_ => { }, typeof(MappingProfile).Assembly);

            // Add HttpContextAccessor
            services.AddHttpContextAccessor();

            services.AddHostedService<EmailOutboxWorker>();
            services.AddHostedService<SubscriptionExpiryWorker>();
            services.AddHostedService<OrderCleanupBackgroundService>();

            return services;
        }
    }
}
