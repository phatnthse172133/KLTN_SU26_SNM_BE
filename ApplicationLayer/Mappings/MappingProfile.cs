using AutoMapper;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using DomainLayer.Entities;

namespace ApplicationLayer.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<UpdateProfileRequest, User>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.Email, o => o.Ignore())
                .ForMember(d => d.UserName, o => o.Ignore())
                .ForMember(d => d.PasswordHash, o => o.Ignore())
                .ForMember(d => d.RoleId, o => o.Ignore())
                .ForMember(d => d.Status, o => o.Ignore())
                .ForMember(d => d.AvatarUrl, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.AuthProvider, o => o.Ignore())
                .ForMember(d => d.GoogleId, o => o.Ignore())
                .ForMember(d => d.RefreshTokenHash, o => o.Ignore())
                .ForMember(d => d.RefreshTokenExpiresAt, o => o.Ignore())
                .ForMember(d => d.EmailVerificationTokenHash, o => o.Ignore())
                .ForMember(d => d.EmailVerificationTokenExpiresAt, o => o.Ignore())
                .ForMember(d => d.PasswordResetOtpHash, o => o.Ignore())
                .ForMember(d => d.PasswordResetOtpExpiresAt, o => o.Ignore())
                .ForMember(d => d.PasswordResetTokenHash, o => o.Ignore())
                .ForMember(d => d.PasswordResetTokenExpiresAt, o => o.Ignore())
                .ForMember(d => d.Booth, o => o.Ignore())
                .ForMember(d => d.Carts, o => o.Ignore())
                .ForMember(d => d.Complaints, o => o.Ignore())
                .ForMember(d => d.Conversations, o => o.Ignore())
                .ForMember(d => d.Messages, o => o.Ignore())
                .ForMember(d => d.Notifications, o => o.Ignore())
                .ForMember(d => d.DeviceTokens, o => o.Ignore())
                .ForMember(d => d.Orders, o => o.Ignore())
                .ForMember(d => d.Payments, o => o.Ignore())
                .ForMember(d => d.PromotionUsages, o => o.Ignore())
                .ForMember(d => d.ReviewReplies, o => o.Ignore())
                .ForMember(d => d.Reviews, o => o.Ignore())
                .ForMember(d => d.CustomerPreferences, o => o.Ignore())
                .ForMember(d => d.AIRecommendationLogs, o => o.Ignore())
                .ForMember(d => d.Role, o => o.Ignore())
                .ForMember(d => d.BoothRegistrations, o => o.Ignore())
                .ForMember(d => d.PaymentMethods, o => o.Ignore());

            CreateMap<CartItem, CartItemResponse>()
                .ForMember(d => d.CartItemId, o => o.MapFrom(s => s.Id))
                .ForMember(d => d.FoodName, o => o.MapFrom(s => s.FoodItem.Name))
                .ForMember(d => d.ThumbnailUrl, o => o.MapFrom(s => s.FoodItem.ThumbnailUrl))
                .ForMember(d => d.IsAvailable, o => o.MapFrom(s =>
                    s.FoodItem.IsAvailable
                    && !s.FoodItem.IsDeleted
                    && !s.FoodItem.Category.IsDeleted
                    && s.FoodItem.Booth.Status == DomainLayer.Enums.GeneralEnum.BoothStatus.Active))
                .ForMember(d => d.CurrentUnitPrice, o => o.Ignore())
                .ForMember(d => d.LineTotal, o => o.Ignore());

            CreateMap<CreatePromotionRequest, Promotion>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.BoothId, o => o.Ignore())
                .ForMember(d => d.PromotionCode, o => o.MapFrom(s =>
                    string.IsNullOrWhiteSpace(s.PromotionCode)
                        ? null
                        : s.PromotionCode.Trim().ToUpperInvariant()))
                .ForMember(d => d.Title, o => o.MapFrom(s => s.Title.Trim()))
                .ForMember(d => d.Description, o => o.MapFrom(s =>
                    ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.Description)))
                .ForMember(d => d.Status, o => o.Ignore())
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.Booth, o => o.Ignore())
                .ForMember(d => d.PromotionUsages, o => o.Ignore())
                .ForMember(d => d.PromotionFoodItems, o => o.Ignore())
                .ForMember(d => d.PromotionCategories, o => o.Ignore());
            CreateMap<UpdatePromotionRequest, Promotion>()
                .IncludeBase<CreatePromotionRequest, Promotion>();
            CreateMap<PromotionFoodItem, PromotionFoodItemResponse>()
                .ForMember(d => d.FoodName, o => o.MapFrom(s => s.FoodItem.Name));
            CreateMap<PromotionCategory, PromotionCategoryResponse>()
                .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category.Name));
            CreateMap<Promotion, PromotionResponse>()
                .ForMember(d => d.BoothName, o => o.MapFrom(s => s.Booth.BoothName))
                .ForMember(d => d.DiscountType, o => o.MapFrom(s => s.DiscountType.ToString()))
                .ForMember(d => d.Scope, o => o.MapFrom(s => s.Scope.ToString()))
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.UsedCount, o => o.MapFrom(s =>
                    s.PromotionUsages.Count(usage =>
                        usage.Status != DomainLayer.Enums.GeneralEnum.PromotionUsageStatus.Released)))
                .ForMember(d => d.FoodItems, o => o.MapFrom(s => s.PromotionFoodItems))
                .ForMember(d => d.Categories, o => o.MapFrom(s => s.PromotionCategories));

            CreateMap<User, ManagedUserResponse>()
                .ForMember(d => d.Role, o => o.Ignore())
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

            CreateMap<User, UserResponse>()
                .ForMember(d => d.Role, o => o.Ignore())
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

            CreateMap<CreateBoothRegistrationRequest, BoothRegistration>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.OwnerId, o => o.Ignore())
                .ForMember(d => d.Status, o => o.Ignore())
                .ForMember(d => d.RejectReason, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.Owner, o => o.Ignore())
                .ForMember(d => d.RequestedNightMarket, o => o.Ignore())
                .ForMember(d => d.PreferredZone, o => o.Ignore())
                .ForMember(d => d.PreferredLayoutNode, o => o.Ignore())
                .ForMember(d => d.BoothDocuments, o => o.Ignore())
                .ForMember(d => d.Booth, o => o.Ignore());

            CreateMap<BoothDocumentRequest, BoothDocument>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.RegistrationId, o => o.Ignore())
                .ForMember(d => d.DocumentUrl, o => o.MapFrom(s => s.FileUrl))
                .ForMember(d => d.VerificationStatus, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.Registration, o => o.Ignore());

            CreateMap<BoothRegistration, BoothRegistrationResponse>()
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.Documents, o => o.MapFrom(s => s.BoothDocuments))
                .ForMember(d => d.BoothId, o => o.MapFrom(s => s.Booth == null ? (Guid?)null : s.Booth.Id));

            CreateMap<BoothDocument, BoothDocumentResponse>()
                .ForMember(d => d.VerificationStatus, o => o.MapFrom(s => s.VerificationStatus.ToString()));

            CreateMap<UpdateMyBoothRequest, Booth>()
                .ForMember(d => d.Id, o => o.Ignore()).ForMember(d => d.RegistrationId, o => o.Ignore())
                .ForMember(d => d.NightMarketId, o => o.Ignore()).ForMember(d => d.BoothOwnerId, o => o.Ignore())
                .ForMember(d => d.ZoneId, o => o.Ignore()).ForMember(d => d.BoothCode, o => o.Ignore())
                .ForMember(d => d.Status, o => o.Ignore()).ForMember(d => d.IsFeatured, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore()).ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.SlotNumber, o => o.Ignore()).ForMember(d => d.MapPositionX, o => o.Ignore())
                .ForMember(d => d.MapPositionY, o => o.Ignore()).ForMember(d => d.Latitude, o => o.Ignore())
                .ForMember(d => d.Longitude, o => o.Ignore()).ForMember(d => d.AverageRating, o => o.Ignore())
                .ForMember(d => d.PackageName, o => o.Ignore()).ForMember(d => d.PackageExpiryDate, o => o.Ignore())
                .ForMember(d => d.BoothDocuments, o => o.Ignore()).ForMember(d => d.BoothImages, o => o.Ignore())
                .ForMember(d => d.BoothLocations, o => o.Ignore()).ForMember(d => d.BoothOwner, o => o.Ignore())
                .ForMember(d => d.BoothPaymentInfos, o => o.Ignore()).ForMember(d => d.Registration, o => o.Ignore())
                .ForMember(d => d.Zone, o => o.Ignore()).ForMember(d => d.BoothSubscriptions, o => o.Ignore())
                .ForMember(d => d.Complaints, o => o.Ignore()).ForMember(d => d.FoodItems, o => o.Ignore())
                .ForMember(d => d.FoodCategories, o => o.Ignore()).ForMember(d => d.NightMarket, o => o.Ignore())
                .ForMember(d => d.Notifications, o => o.Ignore()).ForMember(d => d.Promotions, o => o.Ignore())
                .ForMember(d => d.Reviews, o => o.Ignore());
            CreateMap<AdminUpdateBoothRequest, Booth>().IncludeBase<UpdateMyBoothRequest, Booth>()
                .ForMember(d => d.Id, o => o.Ignore()).ForMember(d => d.RegistrationId, o => o.Ignore())
                .ForMember(d => d.NightMarketId, o => o.Ignore()).ForMember(d => d.BoothOwnerId, o => o.Ignore())
                .ForMember(d => d.BoothCode, o => o.Ignore()).ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore()).ForMember(d => d.Latitude, o => o.Ignore())
                .ForMember(d => d.Longitude, o => o.Ignore()).ForMember(d => d.AverageRating, o => o.Ignore())
                .ForMember(d => d.PackageName, o => o.Ignore()).ForMember(d => d.PackageExpiryDate, o => o.Ignore())
                .ForMember(d => d.BoothDocuments, o => o.Ignore()).ForMember(d => d.BoothImages, o => o.Ignore())
                .ForMember(d => d.BoothLocations, o => o.Ignore()).ForMember(d => d.BoothOwner, o => o.Ignore())
                .ForMember(d => d.BoothPaymentInfos, o => o.Ignore()).ForMember(d => d.Registration, o => o.Ignore())
                .ForMember(d => d.Zone, o => o.Ignore()).ForMember(d => d.BoothSubscriptions, o => o.Ignore())
                .ForMember(d => d.Complaints, o => o.Ignore()).ForMember(d => d.FoodItems, o => o.Ignore())
                .ForMember(d => d.FoodCategories, o => o.Ignore()).ForMember(d => d.NightMarket, o => o.Ignore())
                .ForMember(d => d.Notifications, o => o.Ignore()).ForMember(d => d.Promotions, o => o.Ignore())
                .ForMember(d => d.Reviews, o => o.Ignore());
            CreateMap<Booth, BoothResponse>().ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

            CreateMap<CreateNightMarketRequest, NightMarket>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.Name, o => o.MapFrom(s => s.Name.Trim()))
                .ForMember(d => d.Description, o => o.MapFrom(s => ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.Description)))
                .ForMember(d => d.Address, o => o.MapFrom(s => s.Address.Trim()))
                .ForMember(d => d.ThumbnailUrl, o => o.MapFrom(s => ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.ThumbnailUrl)))
                .ForMember(d => d.TotalBooth, o => o.Ignore())
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.Booths, o => o.Ignore())
                .ForMember(d => d.MarketLayouts, o => o.Ignore())
                .ForMember(d => d.Zones, o => o.Ignore())
                .ForMember(d => d.AIRecommendationLogs, o => o.Ignore())
                .ForMember(d => d.BoothRegistrations, o => o.Ignore());
            CreateMap<UpdateNightMarketRequest, NightMarket>()
                .IncludeBase<CreateNightMarketRequest, NightMarket>();
            CreateMap<UpdateNightMarketGeographicLocationRequest, NightMarket>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.Name, o => o.Ignore())
                .ForMember(d => d.Description, o => o.Ignore())
                .ForMember(d => d.Address, o => o.MapFrom(s => s.Address.Trim()))
                .ForMember(d => d.OpeningHours, o => o.Ignore())
                .ForMember(d => d.ClosingHours, o => o.Ignore())
                .ForMember(d => d.TotalBooth, o => o.Ignore())
                .ForMember(d => d.ThumbnailUrl, o => o.Ignore())
                .ForMember(d => d.Status, o => o.Ignore())
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.Booths, o => o.Ignore())
                .ForMember(d => d.MarketLayouts, o => o.Ignore())
                .ForMember(d => d.Zones, o => o.Ignore())
                .ForMember(d => d.AIRecommendationLogs, o => o.Ignore())
                .ForMember(d => d.BoothRegistrations, o => o.Ignore());
            CreateMap<NightMarket, NightMarketResponse>()
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
            CreateMap<NightMarket, NightMarketNavigationInfoResponse>()
                .ForMember(d => d.NightMarketId, o => o.MapFrom(s => s.Id))
                .ForMember(d => d.Destination, o => o.MapFrom(s => new GeographicCoordinateResponse
                {
                    Latitude = s.Latitude!.Value,
                    Longitude = s.Longitude!.Value
                }))
                .ForMember(d => d.Boundary, o => o.MapFrom(s => new GeographicBoundaryResponse
                {
                    WidthMeters = s.BoundaryWidthMeters!.Value,
                    HeightMeters = s.BoundaryHeightMeters!.Value
                }));

            CreateMap<CreateFoodCategoryRequest, FoodCategory>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.BoothId, o => o.Ignore())
                .ForMember(d => d.Name, o => o.MapFrom(s => s.Name.Trim()))
                .ForMember(d => d.Description, o => o.MapFrom(s => ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.Description)))
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.Booth, o => o.Ignore())
                .ForMember(d => d.FoodItems, o => o.Ignore())
                .ForMember(d => d.PromotionCategories, o => o.Ignore());

            CreateMap<UpdateFoodCategoryRequest, FoodCategory>()
                .IncludeBase<CreateFoodCategoryRequest, FoodCategory>();

            CreateMap<FoodCategory, FoodCategoryResponse>();

            CreateMap<CreateFoodItemRequest, FoodItem>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.BoothId, o => o.Ignore())
                .ForMember(d => d.Name, o => o.MapFrom(s => s.Name.Trim()))
                .ForMember(d => d.Description, o => o.MapFrom(s => ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.Description)))
                .ForMember(d => d.ThumbnailUrl, o => o.MapFrom(s => ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.ThumbnailUrl)))
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.Booth, o => o.Ignore())
                .ForMember(d => d.CartItems, o => o.Ignore())
                .ForMember(d => d.Category, o => o.Ignore())
                .ForMember(d => d.FoodImages, o => o.Ignore())
                .ForMember(d => d.FoodItemTags, o => o.Ignore())
                .ForMember(d => d.FoodPrices, o => o.Ignore())
                .ForMember(d => d.OrderDetails, o => o.Ignore())
                .ForMember(d => d.PromotionFoodItems, o => o.Ignore());
            CreateMap<UpdateFoodItemRequest, FoodItem>()
                .IncludeBase<CreateFoodItemRequest, FoodItem>();
            CreateMap<FoodItem, FoodItemResponse>()
                .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category.Name));

            CreateMap<CreateComplaintRequest, Complaint>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.CustomerId, o => o.Ignore())
                .ForMember(d => d.Title, o => o.MapFrom(s => s.Title.Trim()))
                .ForMember(d => d.Description, o => o.MapFrom(s => s.Description.Trim()))
                .ForMember(d => d.AdminResponse, o => o.Ignore())
                .ForMember(d => d.Status, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.Booth, o => o.Ignore())
                .ForMember(d => d.Customer, o => o.Ignore())
                .ForMember(d => d.Order, o => o.Ignore())
                .ForMember(d => d.ComplaintImages, o => o.Ignore())
                .ForMember(d => d.ResolutionAction, o => o.Ignore())
                .ForMember(d => d.PolicyViolation, o => o.Ignore());
            CreateMap<Complaint, ComplaintResponse>()
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.ResolutionAction, o => o.MapFrom(s => s.ResolutionAction == null ? null : s.ResolutionAction.ToString()))
                .ForMember(d => d.ImageUrls, o => o.MapFrom(s => s.ComplaintImages.Select(i => i.ImageUrl)));

            CreateMap<CreateReviewRequest, Review>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.CustomerId, o => o.Ignore())
                .ForMember(d => d.Content, o => o.MapFrom(s => ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.Content)))
                .ForMember(d => d.ImageUrl, o => o.MapFrom(s => ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.ImageUrl)))
                .ForMember(d => d.IsVisible, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.Booth, o => o.Ignore())
                .ForMember(d => d.Customer, o => o.Ignore())
                .ForMember(d => d.Order, o => o.Ignore())
                .ForMember(d => d.ReviewReply, o => o.Ignore());
            CreateMap<Review, ReviewResponse>()
                .ForMember(d => d.Reply, o => o.MapFrom(s => s.ReviewReply));
            CreateMap<ReviewReply, ReviewReplyResponse>();

            CreateMap<CreateZoneRequest, Zone>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.NightMarketId, o => o.Ignore())
                .ForMember(d => d.ZoneName, o => o.MapFrom(s => s.ZoneName.Trim()))
                .ForMember(d => d.Description, o => o.MapFrom(s => ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.Description)))
                .ForMember(d => d.Color, o => o.MapFrom(s => ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.Color)))
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.NightMarket, o => o.Ignore())
                .ForMember(d => d.BoothRegistrations, o => o.Ignore())
                .ForMember(d => d.BoothLocations, o => o.Ignore())
                .ForMember(d => d.LayoutNodes, o => o.Ignore());
            CreateMap<UpdateZoneRequest, Zone>().IncludeBase<CreateZoneRequest, Zone>();
            CreateMap<Zone, ZoneResponse>().ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

            CreateMap<CreateMarketLayoutRequest, MarketLayout>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.NightMarketId, o => o.Ignore())
                .ForMember(d => d.LayoutName, o => o.MapFrom(s => s.LayoutName.Trim()))
                .ForMember(d => d.LayoutImageUrl, o => o.Ignore())
                .ForMember(d => d.Width, o => o.Ignore())
                .ForMember(d => d.Height, o => o.Ignore())
                .ForMember(d => d.Status, o => o.Ignore())
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.BoothLocations, o => o.Ignore())
                .ForMember(d => d.LayoutNodes, o => o.Ignore())
                .ForMember(d => d.LayoutEdges, o => o.Ignore())
                .ForMember(d => d.NightMarket, o => o.Ignore());
            CreateMap<UpdateMarketLayoutRequest, MarketLayout>()
                .IncludeBase<CreateMarketLayoutRequest, MarketLayout>();
            CreateMap<UpdateMarketLayoutImageRequest, MarketLayout>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.NightMarketId, o => o.Ignore())
                .ForMember(d => d.LayoutName, o => o.Ignore())
                .ForMember(d => d.Version, o => o.Ignore())
                .ForMember(d => d.Status, o => o.Ignore())
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.BoothLocations, o => o.Ignore())
                .ForMember(d => d.LayoutNodes, o => o.Ignore())
                .ForMember(d => d.LayoutEdges, o => o.Ignore())
                .ForMember(d => d.NightMarket, o => o.Ignore());
            CreateMap<MarketLayout, MarketLayoutResponse>()
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
            CreateMap<CreateLayoutNodeRequest, LayoutNode>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.LayoutId, o => o.Ignore())
                .ForMember(d => d.Xcoordinate, o => o.MapFrom(s => s.XCoordinate))
                .ForMember(d => d.Ycoordinate, o => o.MapFrom(s => s.YCoordinate))
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.Layout, o => o.Ignore())
                .ForMember(d => d.Zone, o => o.Ignore())
                .ForMember(d => d.OutgoingEdges, o => o.Ignore())
                .ForMember(d => d.IncomingEdges, o => o.Ignore())
                .ForMember(d => d.BoothLocations, o => o.Ignore());
            CreateMap<UpdateLayoutNodeRequest, LayoutNode>().IncludeBase<CreateLayoutNodeRequest, LayoutNode>();
            CreateMap<LayoutNode, LayoutNodeResponse>()
                .ForMember(d => d.NodeType, o => o.MapFrom(s => s.NodeType.ToString()))
                .ForMember(d => d.XCoordinate, o => o.MapFrom(s => s.Xcoordinate))
                .ForMember(d => d.YCoordinate, o => o.MapFrom(s => s.Ycoordinate));

            CreateMap<CreateLayoutEdgeRequest, LayoutEdge>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.LayoutId, o => o.Ignore())
                .ForMember(d => d.Distance, o => o.Ignore())
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.Layout, o => o.Ignore())
                .ForMember(d => d.FromNode, o => o.Ignore())
                .ForMember(d => d.ToNode, o => o.Ignore());
            CreateMap<UpdateLayoutEdgeRequest, LayoutEdge>().IncludeBase<CreateLayoutEdgeRequest, LayoutEdge>();
            CreateMap<LayoutEdge, LayoutEdgeResponse>();
            CreateMap<BoothLocation, BoothLocationResponse>()
                .ForMember(d => d.XCoordinate, o => o.MapFrom(s => s.Xcoordinate))
                .ForMember(d => d.YCoordinate, o => o.MapFrom(s => s.Ycoordinate));

            CreateMap<CreatePackageRequest, Package>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.PackageName, o => o.MapFrom(s => s.PackageName.Trim()))
                .ForMember(d => d.Description, o => o.MapFrom(s => ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.Description)))
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.PackagePrices, o => o.Ignore())
                .ForMember(d => d.BoothSubscriptions, o => o.Ignore());
            CreateMap<UpdatePackageRequest, Package>().IncludeBase<CreatePackageRequest, Package>();
            CreateMap<Package, PackageResponse>().ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

            CreateMap<CreatePriceRequest, FoodPrice>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.FoodItemId, o => o.Ignore())
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.FoodItem, o => o.Ignore());
            CreateMap<UpdatePriceRequest, FoodPrice>().IncludeBase<CreatePriceRequest, FoodPrice>();
            CreateMap<FoodPrice, FoodPriceResponse>();

            CreateMap<CreatePriceRequest, PackagePrice>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.PackageId, o => o.Ignore())
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.Package, o => o.Ignore());
            CreateMap<UpdatePriceRequest, PackagePrice>().IncludeBase<CreatePriceRequest, PackagePrice>();
            CreateMap<PackagePrice, PackagePriceResponse>();

            CreateMap<Notification, NotificationListItemResponse>()
                .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()));
            CreateMap<Notification, NotificationDetailResponse>()
                .IncludeBase<Notification, NotificationListItemResponse>();
            CreateMap<UserDeviceToken, DeviceTokenResponse>()
                .ForMember(d => d.Platform, o => o.MapFrom(s => s.Platform.ToString()));
        }
    }
}
