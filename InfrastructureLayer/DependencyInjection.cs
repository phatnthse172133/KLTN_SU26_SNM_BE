using InfrastructureLayer.Data;
using Microsoft.Extensions.Options;
using InfrastructureLayer.Cores.Emails;
using InfrastructureLayer.Cores.External;
using InfrastructureLayer.Cores.Helppers;
using InfrastructureLayer.Cores.JWTs;
using InfrastructureLayer.Repositories;
using InfrastructureLayer.Workers;
using ApplicationLayer.Services.Auth;
using ApplicationLayer.Services.Account;
using ApplicationLayer.Services.BoothRegistrations;
using ApplicationLayer.Services.Booths;
using ApplicationLayer.Services.FoodCategories;
using ApplicationLayer.Services.Menus;
using ApplicationLayer.Services.NightMarkets;
using ApplicationLayer.Services.CustomerDiscovery;
using ApplicationLayer.Services.Complaints;
using ApplicationLayer.Services.Reviews;
using ApplicationLayer.Services.Zones;
using ApplicationLayer.Services.MarketLayouts;
using ApplicationLayer.Services.LayoutNodes;
using ApplicationLayer.Services.LayoutEdges;
using ApplicationLayer.Services.BoothLocations;
using ApplicationLayer.Services.MapNavigation;
using ApplicationLayer.Services.NavigationAnchors;
using ApplicationLayer.Services.IndoorPositioning;
using ApplicationLayer.Services.Packages;
using ApplicationLayer.Services.Prices;
using ApplicationLayer.Services.Carts;
using ApplicationLayer.Services.Promotions;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Chats;
using ApplicationLayer.Services.Subscriptions;
using ApplicationLayer.Services.PayOS;
using ApplicationLayer.Configuration;
using ApplicationLayer.Services.Dashboard;
using ApplicationLayer.Services.MarketOwnerDashboard;
using ApplicationLayer.Services.AdminAILog;
using ApplicationLayer.Services.AdminModeration;
using ApplicationLayer.Mappings;
using ApplicationLayer.AI;
using DomainLayer.Entities;
using DomainLayer.InterfaceCore.Email;
using DomainLayer.InterfaceCore.External;
using DomainLayer.InterfaceCore.JWT;
using DomainLayer.InterfaceRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using InfrastructureLayer.Cores.Notifications;
using DomainLayer.InterfaceRepositories;
using ApplicationLayer.Services.PaymentMethods;
using ApplicationLayer.Services.Orders;
using InfrastructureLayer.Cores.AI;
using InfrastructureLayer.Backgrounds;
using ApplicationLayer.AI.Services;
using ApplicationLayer.AI.V2.Configuration;
using ApplicationLayer.AI.V2.Services;

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
            services.AddScoped<IBoothRegistrationRepository, BoothRegistrationRepository>();
            services.AddScoped<IFoodCategoryRepository, FoodCategoryRepository>();
            services.AddScoped<IFoodItemRepository, FoodItemRepository>();
            services.AddScoped<IFoodSemanticMetadataRepository, FoodSemanticMetadataRepository>();
            services.AddScoped<IFoodPriceRepository, FoodPriceRepository>();
            services.AddScoped<IPackagePriceRepository, PackagePriceRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IComplaintRepository, ComplaintRepository>();
            services.AddScoped<IReviewRepository, ReviewRepository>();
            services.AddScoped<INightMarketRepository, NightMarketRepository>();
            services.AddScoped<INightMarketImageRepository, NightMarketImageRepository>();
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
            services.AddScoped<IMessageRepository, MessageRepository>();
            services.AddScoped<IMarketOwnerDashboardRepository, MarketOwnerDashboardRepository>();
            services.AddScoped<IUserDeviceTokenRepository, UserDeviceTokenRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
            services.AddScoped<IFoodTagRepository, FoodTagRepository>();
            services.AddScoped<ICustomerPreferenceRepository, CustomerPreferenceRepository>();
            services.AddScoped<IAIRecommendationLogRepository, AIRecommendationLogRepository>();
            services.AddScoped<IAICustomerContextRepository, AICustomerContextRepository>();
            services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
            services.AddScoped<IJwtService, JWTService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IAccountService, AccountService>();

            services.AddScoped<IBoothRegistrationService, BoothRegistrationService>();
            services.AddScoped<IBoothService, BoothService>();

            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<ICustomerCheckoutService, CustomerCheckoutService>();

            services.AddScoped<IFoodCategoryService, FoodCategoryService>();
            services.AddScoped<IMenuService, MenuService>();
            services.AddScoped<IFoodAiProfileGenerator, FoodAiProfileGenerator>();
            services.AddScoped<IFoodAiProfileRebuildService, FoodAiProfileRebuildService>();
            services.AddScoped<IFoodMetadataCatalogService, FoodMetadataCatalogService>();
            services.AddScoped<ILegacyFoodTagMetadataAdapter, LegacyFoodTagMetadataAdapter>();

            services.AddScoped<INightMarketService, NightMarketService>();
            services.AddScoped<ICustomerDiscoveryService, CustomerDiscoveryService>();
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<IComplaintService, ComplaintService>();
            services.AddScoped<IReviewService, ReviewService>();
            services.AddScoped<IZoneService, ZoneService>();
            services.AddScoped<IMarketLayoutService, MarketLayoutService>();
            services.AddScoped<ILayoutGraphValidationService, LayoutGraphValidationService>();
            services.AddScoped<ILayoutNodeService, LayoutNodeService>();
            services.AddScoped<ILayoutEdgeService, LayoutEdgeService>();
            services.AddScoped<IBoothLocationService, BoothLocationService>();
            services.AddScoped<IMapNavigationService, MapNavigationService>();
            services.AddScoped<IIndoorRouteSolver, DijkstraIndoorRouteSolver>();
            services.AddScoped<IIndoorRouteInstructionBuilder, IndoorRouteInstructionBuilder>();
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
            services.AddScoped<IAdminAILogService, AdminAILogService>();
            services.AddScoped<IPriceService, PriceService>();
            services.AddScoped<ICartService, CartService>();
            services.AddScoped<IPromotionService, PromotionService>();
            services.AddScoped<IPromotionValidationService, PromotionValidationService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IDeviceTokenService, DeviceTokenService>();
            services.AddScoped<IFoodTagService, FoodTagService>();
            services.AddScoped<ICustomerPreferenceService, CustomerPreferenceService>();
            services.AddScoped<ICustomerFoodProfileService, CustomerFoodProfileService>();
            services.AddScoped<ILegacyCustomerPreferenceAdapter, LegacyCustomerPreferenceAdapter>();
            services.AddScoped<IAIRecommendationService, AIRecommendationService>();
            services.AddScoped<IAISettingsService, AISettingsService>();
            services.Configure<AiProviderRuntimeOptions>(configuration.GetSection(AiProviderRuntimeOptions.SectionName));
            services.Configure<GeminiV2SecretOptions>(configuration.GetSection(AiProviderRuntimeOptions.SectionName));
            services.Configure<RecommendationV2Options>(configuration.GetSection(RecommendationV2Options.SectionName));
            services.Configure<MealPlanV2Options>(configuration.GetSection(MealPlanV2Options.SectionName));
            services.AddSingleton<IFoodRecommendationFallbackParser, DeterministicFoodIntentParser>();
            services.AddScoped<IFoodRecommendationIntentNormalizer, FoodRecommendationIntentNormalizer>();
            services.AddSingleton<IDeterministicRecommendationReasonBuilder, DeterministicRecommendationReasonBuilder>();
            services.AddHttpClient<GeminiV2Client>()
                .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan)
                .RedactLoggedHeaders(_ => true)
                .RemoveAllLoggers();
            services.AddScoped<IAiIntentExtractor, GeminiIntentExtractor>();
            services.AddScoped<IAiExplanationGenerator, GeminiExplanationGenerator>();
            services.AddScoped<IFoodRecommendationReadRepository, FoodRecommendationReadRepository>();
            services.AddScoped<IAiRecommendationSessionRepository, AiRecommendationSessionRepository>();
            services.AddSingleton<IFoodSemanticMatcher, DeterministicFoodSemanticMatcher>();
            services.AddSingleton<IFoodRecommendationRanker, FoodRecommendationRanker>();
            services.AddSingleton<IFoodRecommendationDiversityReranker, FoodRecommendationDiversityReranker>();
            services.AddScoped<IFoodRecommendationV2Service, FoodRecommendationV2Service>();
            services.AddScoped<IMealPlanCandidateRepository, MealPlanCandidateRepository>();
            services.AddScoped<IMealPlanV2Repository, MealPlanV2Repository>();
            services.AddSingleton<IMealPlanPolicyResolver, MealPlanPolicyResolver>();
            services.AddSingleton<IMealPlanRecalculationService, MealPlanRecalculationService>();
            services.AddScoped<IMealPlanV2Service, MealPlanV2Service>();
            services.AddScoped<IMealPlanCartIntegrationService, MealPlanCartIntegrationService>();
            services.AddSingleton<IOnlinePresenceService, OnlinePresenceService>();
            services.Configure<AIProviderSettings>(
                configuration.GetSection(AIProviderSettings.SectionName));
            services.AddHttpClient<IAIProviderService, GeminiAIProviderService>()
                .ConfigureHttpClient((serviceProvider, client) =>
                {
                    var settings = serviceProvider.GetRequiredService<IOptions<AIProviderSettings>>().Value;
                    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 30));
                });
            services.Configure<FirebaseSettings>(
                configuration.GetSection(FirebaseSettings.SectionName));
            services.AddHttpClient<IPushNotificationService, FirebasePushNotificationService>();
            services.AddScoped<IPaymentMethodService, PaymentMethodService>();

            // PayOS configuration and service (uses PayOSClient singleton registered in Program.cs)
            services.Configure<PayOSSettings>(configuration.GetSection(PayOSSettings.SectionName));
            services.AddScoped<IPayOSService, PayOSService>();
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
            services.AddHostedService<AiRecommendationSessionCleanupWorker>();
            services.AddHostedService<AiMealPlanSessionCleanupWorker>();

            return services;
        }
    }
}
