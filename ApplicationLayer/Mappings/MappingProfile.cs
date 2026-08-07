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
                .ForMember(d => d.LineTotal, o => o.Ignore())
                .ForMember(d => d.CanOrder, o => o.Ignore())
                .ForMember(d => d.ReasonCode, o => o.Ignore());

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
                .ForMember(d => d.ConfigurationWarnings, o => o.Ignore())
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
                .ForMember(d => d.PreferredLayoutNodeId, o => o.Ignore())
                .ForMember(d => d.PreferredZone, o => o.Ignore())
                .ForMember(d => d.PreferredLayoutNode, o => o.Ignore())
                .ForMember(d => d.BoothDocuments, o => o.Ignore())
                .ForMember(d => d.Booth, o => o.Ignore());

            CreateMap<BoothDocumentRequest, BoothDocument>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.RegistrationId, o => o.Ignore())
                .ForMember(d => d.BoothId, o => o.Ignore())
                .ForMember(d => d.DocumentType, o => o.MapFrom(s => s.DocumentType!.Value))
                .ForMember(d => d.DocumentUrl, o => o.MapFrom(s => s.FileUrl))
                .ForMember(d => d.VerificationStatus, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.Registration, o => o.Ignore())
                .ForMember(d => d.Booth, o => o.Ignore());

            CreateMap<BoothRegistration, BoothRegistrationResponse>()
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.Documents, o => o.MapFrom(s => s.BoothDocuments))
                .ForMember(d => d.BoothId, o => o.MapFrom(s => s.Booth == null ? (Guid?)null : s.Booth.Id))
                .ForMember(d => d.OwnerName, o => o.MapFrom(s => s.Owner == null ? string.Empty : s.Owner.FullName))
                .ForMember(d => d.OwnerEmail, o => o.MapFrom(s => s.Owner == null ? string.Empty : s.Owner.Email));

            CreateMap<BoothDocument, BoothDocumentResponse>()
                .ForMember(d => d.FileUrl, o => o.MapFrom(s => s.DocumentUrl))
                .ForMember(d => d.VerificationStatus, o => o.MapFrom(s => s.VerificationStatus.ToString()));

            CreateMap<UpdateMyBoothRequest, Booth>()
                .ForMember(d => d.Id, o => o.Ignore()).ForMember(d => d.RegistrationId, o => o.Ignore())
                .ForMember(d => d.LogoUrl, o => o.Ignore())
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
                .ForMember(d => d.AiMealPlanItems, o => o.Ignore())
                .ForMember(d => d.Conversations, o => o.Ignore())
                .ForMember(d => d.FoodCategories, o => o.Ignore()).ForMember(d => d.NightMarket, o => o.Ignore())
                .ForMember(d => d.Notifications, o => o.Ignore()).ForMember(d => d.Promotions, o => o.Ignore())
                .ForMember(d => d.Reviews, o => o.Ignore());
            CreateMap<AdminUpdateBoothRequest, Booth>().IncludeBase<UpdateMyBoothRequest, Booth>()
                .ForMember(d => d.Id, o => o.Ignore()).ForMember(d => d.RegistrationId, o => o.Ignore())
                .ForMember(d => d.LogoUrl, o => o.Ignore())
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
                .ForMember(d => d.AiMealPlanItems, o => o.Ignore())
                .ForMember(d => d.FoodCategories, o => o.Ignore()).ForMember(d => d.NightMarket, o => o.Ignore())
                .ForMember(d => d.Notifications, o => o.Ignore()).ForMember(d => d.Promotions, o => o.Ignore())
                .ForMember(d => d.Reviews, o => o.Ignore());
            CreateMap<Booth, BoothResponse>()
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.NightMarketName, o => o.MapFrom(s => s.NightMarket == null ? null : s.NightMarket.Name))
                .ForMember(d => d.ZoneName, o => o.MapFrom(s => s.Zone == null ? null : s.Zone.ZoneName))
                .ForMember(d => d.LogoUrl, o => o.Ignore())
                .ForMember(d => d.BanReason, o => o.Ignore());

            CreateMap<CreateNightMarketRequest, NightMarket>()
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedBy, opt => opt.Ignore())
            .ForMember(dest => dest.DeletionReason, opt => opt.Ignore())
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.Name, o => o.MapFrom(s => s.Name.Trim()))
                .ForMember(d => d.Description, o => o.MapFrom(s => ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.Description)))
                .ForMember(d => d.Address, o => o.MapFrom(s => s.Address.Trim()))
                .ForMember(d => d.ThumbnailUrl, o => o.MapFrom(s => ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.ThumbnailUrl)))
                .ForMember(d => d.TotalBooth, o => o.Ignore())
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.MarketOwnerId, o => o.Ignore())
                .ForMember(d => d.MarketOwner, o => o.Ignore())
                .ForMember(d => d.Booths, o => o.Ignore())
                .ForMember(d => d.MarketLayouts, o => o.Ignore())
                .ForMember(d => d.Zones, o => o.Ignore())
                .ForMember(d => d.NightMarketImages, o => o.Ignore())
                .ForMember(d => d.AIRecommendationLogs, o => o.Ignore())
                .ForMember(d => d.AiMealPlans, o => o.Ignore())
                .ForMember(d => d.BoothRegistrations, o => o.Ignore())
                .ForMember(d => d.ModerationStatus, o => o.Ignore())
                .ForMember(d => d.Status, o => o.Ignore());
            CreateMap<UpdateNightMarketRequest, NightMarket>()
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedBy, opt => opt.Ignore())
            .ForMember(dest => dest.DeletionReason, opt => opt.Ignore())
                .IncludeBase<CreateNightMarketRequest, NightMarket>();
            CreateMap<UpdateNightMarketGeographicLocationRequest, NightMarket>()
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedBy, opt => opt.Ignore())
            .ForMember(dest => dest.DeletionReason, opt => opt.Ignore())
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
                .ForMember(d => d.MarketOwnerId, o => o.Ignore())
                .ForMember(d => d.MarketOwner, o => o.Ignore())
                .ForMember(d => d.Booths, o => o.Ignore())
                .ForMember(d => d.MarketLayouts, o => o.Ignore())
                .ForMember(d => d.Zones, o => o.Ignore())
                .ForMember(d => d.NightMarketImages, o => o.Ignore())
                .ForMember(d => d.AIRecommendationLogs, o => o.Ignore())
                .ForMember(d => d.AiMealPlans, o => o.Ignore())
                .ForMember(d => d.BoothRegistrations, o => o.Ignore())
                .ForMember(d => d.ModerationStatus, o => o.Ignore());
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
                .ForMember(d => d.Code, o => o.Ignore())
                .ForMember(d => d.Name, o => o.MapFrom(s => s.Name.Trim()))
                .ForMember(d => d.Description, o => o.MapFrom(s => ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.Description)))
                .ForMember(d => d.IsSystem, o => o.Ignore())
                .ForMember(d => d.IsActive, o => o.Ignore())
                .ForMember(d => d.DisplayOrder, o => o.Ignore())
                .ForMember(d => d.IsSelectable, o => o.Ignore())
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
                .ForSourceMember(s => s.TagIds, o => o.DoNotValidate())
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
                .ForMember(d => d.PromotionFoodItems, o => o.Ignore())
                .ForMember(d => d.SpiceLevel, o => o.Ignore())
                .ForMember(d => d.ServingTemperature, o => o.Ignore())
                .ForMember(d => d.EstimatedServingCount, o => o.Ignore())
                .ForMember(d => d.ServingSizeDescription, o => o.Ignore())
                .ForMember(d => d.IsShareable, o => o.Ignore())
                .ForMember(d => d.SemanticProfileVersion, o => o.Ignore())
                .ForMember(d => d.SemanticProfileUpdatedAt, o => o.Ignore())
                .ForMember(d => d.Ingredients, o => o.Ignore())
                .ForMember(d => d.Allergens, o => o.Ignore())
                .ForMember(d => d.DietaryAttributes, o => o.Ignore())
                .ForMember(d => d.PreparationMethods, o => o.Ignore())
                .ForMember(d => d.TasteProfiles, o => o.Ignore())
                .ForMember(d => d.SearchFacets, o => o.Ignore())
                .ForMember(d => d.Courses, o => o.Ignore())
                .ForMember(d => d.DiningPurposes, o => o.Ignore())
                .ForMember(d => d.AiProfile, o => o.Ignore());
            CreateMap<UpdateFoodItemRequest, FoodItem>()
                .IncludeBase<CreateFoodItemRequest, FoodItem>();
            CreateMap<FoodItem, FoodItemResponse>()
                .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category.Name))
                .ForMember(d => d.TagIds, o => o.MapFrom(s => s.FoodItemTags.Select(tag => tag.FoodTagId)));

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
                .ForMember(d => d.BoothName, o => o.MapFrom(s => s.Booth != null ? s.Booth.BoothName : null))
                .ForMember(d => d.CustomerName, o => o.MapFrom(s => s.Customer != null ? s.Customer.FullName : null))
                .ForMember(d => d.OrderCode, o => o.MapFrom(s => s.Order != null ? s.Order.OrderCode.ToString() : null))
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
                .ForMember(d => d.LayoutNodes, o => o.Ignore())
                .ForMember(d => d.WidthMeters, o => o.Ignore())
                .ForMember(d => d.LengthMeters, o => o.Ignore())
                .ForMember(d => d.BoothWidthMeters, o => o.Ignore())
                .ForMember(d => d.BoothLengthMeters, o => o.Ignore())
                .ForMember(d => d.HorizontalGapMeters, o => o.Ignore())
                .ForMember(d => d.VerticalGapMeters, o => o.Ignore());
            CreateMap<UpdateZoneRequest, Zone>().IncludeBase<CreateZoneRequest, Zone>();
            CreateMap<Zone, ZoneResponse>()
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.AssignedSlotCount, o => o.MapFrom(s =>
                    s.BoothLocations.Count(location => !location.IsDeleted && location.ReleasedAt == null)));

            CreateMap<CreateMarketLayoutRequest, MarketLayout>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.NightMarketId, o => o.Ignore())
                .ForMember(d => d.LayoutName, o => o.MapFrom(s => s.LayoutName.Trim()))
                .ForMember(d => d.LayoutImageUrl, o => o.Ignore())
                .ForMember(d => d.Version, o => o.Ignore())
                .ForMember(d => d.Width, o => o.Ignore())
                .ForMember(d => d.Height, o => o.Ignore())
                .ForMember(d => d.CoordinateUnit, o => o.Ignore())
                .ForMember(d => d.MetersPerLayoutUnit, o => o.Ignore())
                .ForMember(d => d.DistanceCalibrationStatus, o => o.Ignore())
                .ForMember(d => d.GraphRevision, o => o.Ignore())
                .ForMember(d => d.Status, o => o.Ignore())
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.BoothLocations, o => o.Ignore())
                .ForMember(d => d.LayoutNodes, o => o.Ignore())
                .ForMember(d => d.LayoutEdges, o => o.Ignore())
                .ForMember(d => d.NavigationAnchors, o => o.Ignore())
                .ForMember(d => d.MarketWidthMeters, o => o.Ignore())
                .ForMember(d => d.MarketLengthMeters, o => o.Ignore())
                .ForMember(d => d.PixelsPerMeter, o => o.Ignore())
                .ForMember(d => d.NightMarket, o => o.Ignore());
            CreateMap<UpdateMarketLayoutRequest, MarketLayout>()
                .IncludeBase<CreateMarketLayoutRequest, MarketLayout>();
            CreateMap<UpdateMarketLayoutImageRequest, MarketLayout>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.NightMarketId, o => o.Ignore())
                .ForMember(d => d.LayoutName, o => o.Ignore())
                .ForMember(d => d.Version, o => o.Ignore())
                .ForMember(d => d.CoordinateUnit, o => o.Ignore())
                .ForMember(d => d.MetersPerLayoutUnit, o => o.Ignore())
                .ForMember(d => d.DistanceCalibrationStatus, o => o.Ignore())
                .ForMember(d => d.GraphRevision, o => o.Ignore())
                .ForMember(d => d.Status, o => o.Ignore())
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.BoothLocations, o => o.Ignore())
                .ForMember(d => d.LayoutNodes, o => o.Ignore())
                .ForMember(d => d.LayoutEdges, o => o.Ignore())
                .ForMember(d => d.NavigationAnchors, o => o.Ignore())
                .ForMember(d => d.NightMarket, o => o.Ignore());
            CreateMap<MarketLayout, MarketLayoutResponse>()
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.CoordinateUnit, o => o.MapFrom(s => s.CoordinateUnit.ToString()))
                .ForMember(d => d.DistanceCalibrationStatus, o => o.MapFrom(s => s.DistanceCalibrationStatus.ToString()));
            CreateMap<LayoutBlock, LayoutBlockResponse>()
                .ForMember(d => d.ZoneName, o => o.MapFrom(s => s.Zone == null ? null : s.Zone.ZoneName))
                .ForMember(d => d.ZoneCode, o => o.MapFrom(s => s.Zone == null ? null : s.Zone.ZoneCode))
                .ForMember(d => d.ZoneColor, o => o.MapFrom(s => s.Zone == null ? null : s.Zone.Color))
                .ForMember(d => d.ZoneType, o => o.Ignore())
                .ForMember(d => d.Capacity, o => o.MapFrom(s => s.Zone == null ? 0 : s.Zone.Capacity))
                .ForMember(d => d.SlotCount, o => o.Ignore());
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
                .ForMember(d => d.LayoutBlock, o => o.Ignore())
                .ForMember(d => d.OutgoingEdges, o => o.Ignore())
                .ForMember(d => d.IncomingEdges, o => o.Ignore())
                .ForMember(d => d.BoothLocations, o => o.Ignore())
                .ForMember(d => d.NavigationAnchors, o => o.Ignore());
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
            CreateMap<LayoutEdge, LayoutEdgeResponse>()
                .ForMember(d => d.DistanceMeters, o => o.MapFrom(s => s.Distance));
            CreateMap<BoothLocation, BoothLocationResponse>()
                .ForMember(d => d.XCoordinate, o => o.MapFrom(s => s.Xcoordinate))
                .ForMember(d => d.YCoordinate, o => o.MapFrom(s => s.Ycoordinate));


            CreateMap<CreatePackagePolicyRequest, PackagePolicy>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.PackageId, o => o.Ignore())
                .ForMember(d => d.Version, o => o.Ignore())
                .ForMember(d => d.ContentJson, o => o.Ignore())
                .ForMember(d => d.ContentMarkdown, o => o.Ignore())
                .ForMember(d => d.EffectiveFrom, o => o.Ignore())
                .ForMember(d => d.IsActive, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.Package, o => o.Ignore());
            CreateMap<PackagePolicy, PackagePolicyResponse>()
                .ForMember(d => d.DisplayVersion, o => o.Ignore())
                .ForMember(d => d.Terms, o => o.Ignore());

            CreateMap<CreatePackageRequest, Package>()
            .ForMember(dest => dest.Policies, opt => opt.Ignore())
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.PackageName, o => o.MapFrom(s => s.PackageName.Trim()))
                .ForMember(d => d.Code, o => o.Ignore())
                .ForMember(d => d.Description, o => o.MapFrom(s => ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.Description)))
                .ForMember(d => d.ImageUrl, o => o.Ignore())
                .ForMember(d => d.Entitlements, o => o.Ignore())
                .ForMember(d => d.Type, o => o.Ignore())
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.PackagePrices, o => o.Ignore())
                .ForMember(d => d.BoothSubscriptions, o => o.Ignore())
                .ForMember(d => d.MarketSubscriptions, o => o.Ignore());
            CreateMap<Package, PackageResponse>()
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.Type, o => o.MapFrom(s => s.Type))
                .ForMember(d => d.Features, o => o.MapFrom(s => ApplicationLayer.DTOs.PackageTemplateHelper.GetFeatures(s.Code)));

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
                .ForMember(d => d.Package, o => o.Ignore())
                .ForMember(d => d.DurationDays, o => o.MapFrom(s => s.DurationDays ?? 30));
            CreateMap<UpdatePriceRequest, PackagePrice>().IncludeBase<CreatePriceRequest, PackagePrice>();
            CreateMap<PackagePrice, PackagePriceResponse>();

            CreateMap<Notification, NotificationListItemResponse>()
                .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()));
            CreateMap<Notification, NotificationDetailResponse>()
                .IncludeBase<Notification, NotificationListItemResponse>();
            CreateMap<UserDeviceToken, DeviceTokenResponse>()
                .ForMember(d => d.Platform, o => o.MapFrom(s => s.Platform.ToString()));

            CreateMap<User, ConversationUserResponse>()
                .ForMember(d => d.UserId, o => o.MapFrom(s => s.Id));
            CreateMap<Booth, ConversationBoothResponse>()
                .ForMember(d => d.BoothId, o => o.MapFrom(s => s.Id));
            CreateMap<Message, MessageResponse>()
                .ForMember(d => d.SenderName, o => o.MapFrom(s => s.Sender.FullName))
                .ForMember(d => d.SenderAvatarUrl, o => o.MapFrom(s => s.Sender.AvatarUrl));
            CreateMap<Conversation, ConversationResponse>()
                .ForMember(d => d.Customer, o => o.MapFrom(s => s.Customer))
                .ForMember(d => d.BoothOwnerId, o => o.MapFrom(s => s.Booth.BoothOwnerId))
                .ForMember(d => d.BoothOwner, o => o.MapFrom(s => s.Booth.BoothOwner))
                .ForMember(d => d.Booth, o => o.MapFrom(s => s.Booth))
                .ForMember(d => d.UnreadCount, o => o.Ignore());
        }
    }
}
