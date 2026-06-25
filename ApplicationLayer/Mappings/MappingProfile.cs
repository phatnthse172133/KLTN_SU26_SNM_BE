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
                .ForMember(d => d.UpdatedAt, o => o.Ignore());

            CreateMap<User, ManagedUserResponse>()
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
                .ForMember(d => d.CreatedAt, o => o.Ignore()).ForMember(d => d.UpdatedAt, o => o.Ignore());
            CreateMap<AdminUpdateBoothRequest, Booth>().IncludeBase<UpdateMyBoothRequest, Booth>()
                .ForMember(d => d.Id, o => o.Ignore()).ForMember(d => d.RegistrationId, o => o.Ignore())
                .ForMember(d => d.NightMarketId, o => o.Ignore()).ForMember(d => d.BoothOwnerId, o => o.Ignore())
                .ForMember(d => d.BoothCode, o => o.Ignore()).ForMember(d => d.CreatedAt, o => o.Ignore()).ForMember(d => d.UpdatedAt, o => o.Ignore());
            CreateMap<Booth, BoothResponse>().ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

            CreateMap<CreateNightMarketRequest, NightMarket>()
                .ForMember(d => d.Id, o => o.Ignore()).ForMember(d => d.TotalBooth, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore()).ForMember(d => d.UpdatedAt, o => o.Ignore());
            CreateMap<UpdateNightMarketRequest, NightMarket>()
                .ForMember(d => d.Id, o => o.Ignore()).ForMember(d => d.TotalBooth, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore()).ForMember(d => d.UpdatedAt, o => o.Ignore());
            CreateMap<NightMarket, NightMarketResponse>().ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

            CreateMap<CreateFoodCategoryRequest, FoodCategory>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.BoothId, o => o.Ignore())
                .ForMember(d => d.Name, o => o.MapFrom(s => s.Name.Trim()))
                .ForMember(d => d.Description, o => o.MapFrom(s => ApplicationLayer.Helppers.TextHelper.NormalizeOptionalText(s.Description)))
                .ForMember(d => d.IsDeleted, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.Booth, o => o.Ignore())
                .ForMember(d => d.FoodItems, o => o.Ignore());
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
                .ForMember(d => d.Category, o => o.Ignore())
                .ForMember(d => d.FoodImages, o => o.Ignore())
                .ForMember(d => d.FoodPrices, o => o.Ignore())
                .ForMember(d => d.OrderDetails, o => o.Ignore());
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
                .ForMember(d => d.ComplaintImages, o => o.Ignore());
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
        }
    }
}
