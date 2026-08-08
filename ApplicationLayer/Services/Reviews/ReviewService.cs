using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Storage;
using ApplicationLayer.Services.Subscriptions;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Reviews;

public class ReviewService : IReviewService
{
    private readonly IReviewRepository _reviews;
    private readonly IFoodReviewRepository _foodReviews;
    private readonly IBoothRepository _booths;
    private readonly IOrderRepository _orders;
    private readonly IFoodItemRepository _foodItems;
    private readonly IMapper _mapper;
    private readonly INotificationService _notifications;
    private readonly ISubscriptionEntitlementService _entitlements;
    private readonly IConfiguration _configuration;
    private readonly IFileStorageService _fileStorage;

    public ReviewService(
        IReviewRepository reviews,
        IBoothRepository booths,
        IOrderRepository orders,
        IMapper mapper,
        INotificationService notifications,
        ISubscriptionEntitlementService entitlements,
        IConfiguration configuration,
        IFoodReviewRepository foodReviews,
        IFoodItemRepository foodItems,
        IFileStorageService fileStorage)
    {
        _reviews = reviews;
        _booths = booths;
        _orders = orders;
        _mapper = mapper;
        _notifications = notifications;
        _entitlements = entitlements;
        _configuration = configuration;
        _foodReviews = foodReviews;
        _foodItems = foodItems;
        _fileStorage = fileStorage;
    }

    public async Task<ApiResponse<CustomerReviewHistoryResponse>> CreateAsync(Guid customerId, CreateReviewRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRating(request.Rating);
        NormalizeAndValidateContent(request);
        var boothId = await GetAuthoritativeBoothIdAsync(customerId, request.OrderId, requireCompletedOrder: true, cancellationToken);
        if (request.BoothId != Guid.Empty && request.BoothId != boothId)
            throw AppException.BadRequest("The booth does not match the order.", "ORDER_BOOTH_MISMATCH");

        if (await _reviews.ExistsByOrderAsync(request.OrderId))
            throw AppException.Conflict("This order has already been reviewed.");

        var now = DateTime.UtcNow;
        var review = _mapper.Map<Review>(request);
        review.Id = Guid.NewGuid();
        review.CustomerId = customerId;
        review.BoothId = boothId;
        review.IsVisible = true;
        review.CreatedAt = now;
        review.UpdatedAt = now;

        await _reviews.AddAsync(review);
        if (!await _reviews.TrySaveNewReviewAsync(cancellationToken))
            throw AppException.Conflict("This order has already been reviewed.", "REVIEW_ALREADY_EXISTS");
        await _reviews.RefreshBoothAverageRatingAsync(boothId);
        var booth = await _booths.GetByIdAsync(boothId);
        if (booth is not null)
        {
            await TryNotifyAsync(new NotificationMessage(
                booth.BoothOwnerId,
                NotificationType.NewReview,
                "New booth review",
                $"Your booth received a {review.Rating}-star review.",
                booth.Id,
                "Review",
                review.Id,
                JsonSerializer.Serialize(new
                {
                    reviewId = review.Id,
                    boothId = booth.Id
                })), cancellationToken);
        }

        var created = await _reviews.GetWithReplyByIdAsync(review.Id) ?? review;
        return ApiResponse<CustomerReviewHistoryResponse>.SuccessResponse(ToCustomerResponse(created), "Review created successfully.");
    }

    public async Task<ApiResponse<CustomerReviewHistoryResponse>> UpdateAsync(Guid customerId, Guid reviewId, UpdateReviewRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRating(request.Rating);
        NormalizeAndValidateContent(request);
        var review = await _reviews.GetWithReplyByIdAsync(reviewId);
        if (review is null || review.CustomerId != customerId)
            throw AppException.NotFound("Review was not found.", "REVIEW_NOT_FOUND");

        EnsureWithinEditWindow(review.CreatedAt);

        review.Rating = request.Rating;
        review.Content = TextHelper.NormalizeOptionalText(request.Content);
        review.ImageUrl = TextHelper.NormalizeOptionalText(request.ImageUrl);
        review.UpdatedAt = DateTime.UtcNow;
        _reviews.Update(review);
        await _reviews.SaveChangesAsync();
        await _reviews.RefreshBoothAverageRatingAsync(review.BoothId);

        var updated = await _reviews.GetWithReplyByIdAsync(review.Id);
        return ApiResponse<CustomerReviewHistoryResponse>.SuccessResponse(ToCustomerResponse(updated!), "Review updated successfully.");
    }

    public async Task<ApiResponse<CustomerReviewHistoryResponse>> HideAsync(Guid customerId, Guid reviewId, CancellationToken cancellationToken = default)
    {
        var review = await _reviews.GetWithReplyByIdAsync(reviewId);
        if (review is null || review.CustomerId != customerId)
            throw AppException.NotFound("Review was not found.", "REVIEW_NOT_FOUND");

        if (!review.IsVisible)
            return ApiResponse<CustomerReviewHistoryResponse>.SuccessResponse(ToCustomerResponse(review), "Review is already hidden.");

        review.IsVisible = false;
        review.UpdatedAt = DateTime.UtcNow;
        _reviews.Update(review);
        await _reviews.SaveChangesAsync();
        await _reviews.RefreshBoothAverageRatingAsync(review.BoothId);

        var updated = await _reviews.GetWithReplyByIdAsync(review.Id);
        return ApiResponse<CustomerReviewHistoryResponse>.SuccessResponse(ToCustomerResponse(updated!), "Review hidden successfully.");
    }

    public async Task<ApiResponse<PaginationResp<CustomerReviewResponse>>> GetByBoothAsync(Guid boothId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        if (!await _booths.CustomerVisibleExistsAsync(boothId, cancellationToken))
            throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");

        var page = await _reviews.GetPagedVisibleByBoothWithReplyAsync(
            boothId, pagination.Page, pagination.PageSize, cancellationToken);
        var items = page.Items.Select(review => new CustomerReviewResponse
        {
            Id = review.Id,
            CustomerName = review.Customer.FullName,
            Rating = review.Rating,
            Content = review.Content,
            ImageUrl = review.ImageUrl,
            CreatedAt = review.CreatedAt,
            Reply = review.ReviewReply is null ? null : new CustomerReviewReplyResponse
            {
                Content = review.ReviewReply.Content,
                CreatedAt = review.ReviewReply.CreatedAt
            }
        }).ToList();
        return ApiResponse<PaginationResp<CustomerReviewResponse>>.SuccessResponse(
            PaginationResp<CustomerReviewResponse>.Create(items, page.TotalCount, pagination));
    }

    public async Task<ApiResponse<PaginationResp<CustomerReviewHistoryResponse>>> GetMineAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var page = await _reviews.GetPagedByCustomerWithReplyAsync(
            customerId, pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<CustomerReviewHistoryResponse>>.SuccessResponse(
            PaginationResp<CustomerReviewHistoryResponse>.Create(
                page.Items.Select(ToCustomerResponse).ToList(), page.TotalCount, pagination));
    }

    public async Task<ApiResponse<PaginationResp<ReviewResponse>>> GetAllAsync(PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var page = await _reviews.GetPagedWithReplyAsync(
            pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<ReviewResponse>>.SuccessResponse(
            _mapper.MapPage<Review, ReviewResponse>(page, pagination));
    }

    public async Task<ApiResponse<PaginationResp<ReviewResponse>>> GetAllFilteredAsync(AdminReviewQueryRequest query, CancellationToken cancellationToken = default)
    {
        var page = await _reviews.GetPagedWithReplyFilteredAsync(
            query.Page, query.PageSize, query.Rating, query.IsVisible, query.BoothId, query.Keyword, cancellationToken);
        return ApiResponse<PaginationResp<ReviewResponse>>.SuccessResponse(
            _mapper.MapPage<Review, ReviewResponse>(page, new PaginationReq { Page = query.Page, PageSize = query.PageSize }));
    }

    public async Task<ApiResponse<ReviewResponse>> UpdateVisibilityAsync(Guid reviewId, UpdateReviewVisibilityRequest request, CancellationToken cancellationToken = default)
    {
        var review = await _reviews.GetByIdAsync(reviewId);
        if (review is null)
            throw AppException.NotFound("Review was not found.");

        review.IsVisible = request.IsVisible;
        review.UpdatedAt = DateTime.UtcNow;

        _reviews.Update(review);
        await _reviews.SaveChangesAsync();
        await _reviews.RefreshBoothAverageRatingAsync(review.BoothId);

        var response = await _reviews.GetWithReplyByIdAsync(review.Id);
        return ApiResponse<ReviewResponse>.SuccessResponse(ToResponse(response!), request.IsVisible ? "Review is now visible." : "Review is now hidden.");
    }

    public async Task<ApiResponse<ReviewResponse>> UpsertReplyAsync(Guid ownerId, Guid reviewId, UpsertReviewReplyRequest request, CancellationToken cancellationToken = default)
    {
        var review = await _reviews.GetWithReplyByIdAsync(reviewId);
        if (review is null)
            throw AppException.NotFound("Review was not found.");

        var booth = await _booths.GetByIdAsync(review.BoothId);
        if (booth is null)
            throw AppException.NotFound("Booth was not found.");

        if (booth.BoothOwnerId != ownerId)
            throw AppException.Forbidden("You do not have permission to reply to this review.");

        await _entitlements.RequireBoothFeatureAsync(
            booth.Id,
            entitlement => entitlement.ReviewReply,
            "Your current plan does not include review replies. Upgrade to Booth Boost or Booth Featured to reply.",
            "REVIEW_REPLY_NOT_INCLUDED");

        var content = TextHelper.NormalizeOptionalText(request.Content);
        if (content is null)
            throw AppException.BadRequest("Reply content is required.", "REVIEW_REPLY_REQUIRED");

        if (review.ReviewReply?.Content == content)
            return ApiResponse<ReviewResponse>.SuccessResponse(ToResponse(review), "Review reply is unchanged.");

        await _reviews.UpsertReplyAsync(reviewId, ownerId, content);
        await _reviews.SaveChangesAsync();
        await TryNotifyAsync(new NotificationMessage(
            review.CustomerId,
            NotificationType.ReviewReplied,
            "Booth replied to your review",
            content,
            review.BoothId,
            "Review",
            review.Id,
            JsonSerializer.Serialize(new
            {
                reviewId = review.Id,
                boothId = review.BoothId
            })), cancellationToken);

        var response = await _reviews.GetWithReplyByIdAsync(review.Id);
        return ApiResponse<ReviewResponse>.SuccessResponse(ToResponse(response!), "Review reply saved successfully.");
    }

    public async Task<ApiResponse<CustomerFoodReviewHistoryResponse>> CreateFoodReviewAsync(Guid customerId, CreateFoodReviewRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRating(request.Rating);
        NormalizeAndValidateFoodContent(request.Content, request.ImageUrl);
        request.Content = TextHelper.NormalizeOptionalText(request.Content);
        request.ImageUrl = TextHelper.NormalizeOptionalText(request.ImageUrl);

        var orderDetail = await _orders.GetCustomerOrderDetailLineAsync(customerId, request.OrderDetailId, cancellationToken)
            ?? throw AppException.NotFound("Order detail was not found.", "ORDER_DETAIL_NOT_FOUND");

        if (orderDetail.Order.Status != OrderStatus.Completed)
            throw AppException.BadRequest("Only completed orders can be reviewed.");

        if (await _foodReviews.ExistsByOrderDetailAsync(request.OrderDetailId, cancellationToken))
            throw AppException.Conflict("This order item has already been reviewed.", "FOOD_REVIEW_ALREADY_EXISTS");

        var now = DateTime.UtcNow;
        var foodReview = new FoodReview
        {
            Id = Guid.NewGuid(),
            OrderDetailId = orderDetail.Id,
            OrderId = orderDetail.OrderId,
            FoodItemId = orderDetail.FoodItemId,
            CustomerId = customerId,
            BoothId = orderDetail.FoodItem.BoothId,
            Rating = request.Rating,
            Content = request.Content,
            ImageUrl = request.ImageUrl,
            IsVisible = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _foodReviews.AddAsync(foodReview);
        if (!await _foodReviews.TrySaveNewFoodReviewAsync(cancellationToken))
            throw AppException.Conflict("This order item has already been reviewed.", "FOOD_REVIEW_ALREADY_EXISTS");

        await _foodReviews.RefreshFoodItemAverageRatingAsync(foodReview.FoodItemId, cancellationToken);

        var created = await _foodReviews.GetByIdWithNavAsync(foodReview.Id, cancellationToken) ?? foodReview;
        return ApiResponse<CustomerFoodReviewHistoryResponse>.SuccessResponse(ToFoodCustomerResponse(created), "Food review created successfully.");
    }

    public async Task<ApiResponse<CustomerFoodReviewHistoryResponse>> UpdateFoodReviewAsync(Guid customerId, Guid foodReviewId, UpdateFoodReviewRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRating(request.Rating);
        NormalizeAndValidateFoodContent(request.Content, request.ImageUrl);
        var foodReview = await _foodReviews.GetByIdWithNavAsync(foodReviewId, cancellationToken);
        if (foodReview is null || foodReview.CustomerId != customerId)
            throw AppException.NotFound("Food review was not found.", "FOOD_REVIEW_NOT_FOUND");

        EnsureWithinEditWindow(foodReview.CreatedAt);

        foodReview.Rating = request.Rating;
        foodReview.Content = TextHelper.NormalizeOptionalText(request.Content);
        foodReview.ImageUrl = TextHelper.NormalizeOptionalText(request.ImageUrl);
        foodReview.UpdatedAt = DateTime.UtcNow;
        _foodReviews.Update(foodReview);
        await _foodReviews.SaveChangesAsync();
        await _foodReviews.RefreshFoodItemAverageRatingAsync(foodReview.FoodItemId, cancellationToken);

        var updated = await _foodReviews.GetByIdWithNavAsync(foodReview.Id, cancellationToken);
        return ApiResponse<CustomerFoodReviewHistoryResponse>.SuccessResponse(ToFoodCustomerResponse(updated!), "Food review updated successfully.");
    }

    public async Task<ApiResponse<CustomerFoodReviewHistoryResponse>> HideFoodReviewAsync(Guid customerId, Guid foodReviewId, CancellationToken cancellationToken = default)
    {
        var foodReview = await _foodReviews.GetByIdWithNavAsync(foodReviewId, cancellationToken);
        if (foodReview is null || foodReview.CustomerId != customerId)
            throw AppException.NotFound("Food review was not found.", "FOOD_REVIEW_NOT_FOUND");

        if (!foodReview.IsVisible)
            return ApiResponse<CustomerFoodReviewHistoryResponse>.SuccessResponse(ToFoodCustomerResponse(foodReview), "Food review is already hidden.");

        foodReview.IsVisible = false;
        foodReview.UpdatedAt = DateTime.UtcNow;
        _foodReviews.Update(foodReview);
        await _foodReviews.SaveChangesAsync();
        await _foodReviews.RefreshFoodItemAverageRatingAsync(foodReview.FoodItemId, cancellationToken);

        var updated = await _foodReviews.GetByIdWithNavAsync(foodReview.Id, cancellationToken);
        return ApiResponse<CustomerFoodReviewHistoryResponse>.SuccessResponse(ToFoodCustomerResponse(updated!), "Food review hidden successfully.");
    }

    public async Task<ApiResponse<PaginationResp<FoodReviewResponse>>> GetByFoodItemAsync(Guid foodItemId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var foodItem = await _foodItems.GetByIdAsync(foodItemId);
        if (foodItem is null || foodItem.IsDeleted)
            throw AppException.NotFound("Food item was not found.", "FOOD_ITEM_NOT_FOUND");

        var page = await _foodReviews.GetPagedVisibleByFoodItemAsync(foodItemId, pagination.Page, pagination.PageSize, cancellationToken);
        var items = page.Items.Select(ToFoodPublicResponse).ToList();
        return ApiResponse<PaginationResp<FoodReviewResponse>>.SuccessResponse(
            PaginationResp<FoodReviewResponse>.Create(items, page.TotalCount, pagination));
    }

    public async Task<ApiResponse<PaginationResp<CustomerFoodReviewHistoryResponse>>> GetMineFoodAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var page = await _foodReviews.GetPagedByCustomerAsync(customerId, pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<CustomerFoodReviewHistoryResponse>>.SuccessResponse(
            PaginationResp<CustomerFoodReviewHistoryResponse>.Create(
                page.Items.Select(ToFoodCustomerResponse).ToList(), page.TotalCount, pagination));
    }

    public async Task<ApiResponse<ReviewImageUploadResponse>> UploadImageAsync(Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        var imageUrl = await _fileStorage.SaveImageAsync("reviews", stream, fileName, contentType, length, cancellationToken);
        return ApiResponse<ReviewImageUploadResponse>.SuccessResponse(new ReviewImageUploadResponse { ImageUrl = imageUrl }, "Review image uploaded successfully.");
    }

    private int EditWindowDays => Math.Max(1, _configuration.GetValue("ReviewSettings:EditWindowDays", 7));

    private void EnsureWithinEditWindow(DateTime createdAt)
    {
        var deadline = createdAt.AddDays(EditWindowDays);
        if (DateTime.UtcNow > deadline)
            throw AppException.BadRequest("The review edit window has expired.", "REVIEW_EDIT_WINDOW_EXPIRED");
    }

    private (bool CanEdit, DateTime EditDeadline) GetEditState(DateTime createdAt, bool isVisible)
    {
        var deadline = createdAt.AddDays(EditWindowDays);
        return (isVisible && DateTime.UtcNow <= deadline, deadline);
    }

    private async Task TryNotifyAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.NotifyAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Review was persisted but notification delivery failed: {exception.Message}");
        }
    }

    private async Task<Guid> GetAuthoritativeBoothIdAsync(Guid customerId, Guid orderId, bool requireCompletedOrder, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByCustomerAsync(customerId, orderId);
        if (order is null)
            throw AppException.NotFound("Order was not found.");

        if (requireCompletedOrder && order.Status != OrderStatus.Completed)
            throw AppException.BadRequest("Only completed orders can be reviewed.");

        return await _orders.GetBoothIdForCustomerOrderAsync(customerId, orderId, cancellationToken)
            ?? throw AppException.BadRequest("Order has no booth items.", "ORDER_HAS_NO_ITEMS");
    }

    private ReviewResponse ToResponse(Review review)
        => _mapper.Map<ReviewResponse>(review);

    private CustomerReviewHistoryResponse ToCustomerResponse(Review review)
    {
        var edit = GetEditState(review.CreatedAt, review.IsVisible);
        return new()
        {
            ReviewId = review.Id,
            OrderId = review.OrderId,
            OrderCode = review.Order?.OrderCode.ToString(),
            BoothId = review.BoothId,
            BoothName = review.Booth?.BoothName,
            Rating = review.Rating,
            Content = review.Content,
            ImageUrl = review.ImageUrl,
            IsVisible = review.IsVisible,
            HasReply = review.ReviewReply is not null,
            Reply = review.ReviewReply is null ? null : new CustomerReviewReplyResponse
            {
                Content = review.ReviewReply.Content,
                CreatedAt = review.ReviewReply.CreatedAt
            },
            CanEdit = edit.CanEdit,
            EditDeadline = edit.EditDeadline,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt
        };
    }

    private CustomerFoodReviewHistoryResponse ToFoodCustomerResponse(FoodReview review)
    {
        var edit = GetEditState(review.CreatedAt, review.IsVisible);
        return new()
        {
            FoodReviewId = review.Id,
            OrderDetailId = review.OrderDetailId,
            OrderId = review.OrderId,
            OrderCode = review.Order?.OrderCode.ToString(),
            FoodItemId = review.FoodItemId,
            FoodItemName = review.FoodItem?.Name,
            BoothId = review.BoothId,
            BoothName = review.Booth?.BoothName,
            Rating = review.Rating,
            Content = review.Content,
            ImageUrl = review.ImageUrl,
            IsVisible = review.IsVisible,
            CanEdit = edit.CanEdit,
            EditDeadline = edit.EditDeadline,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt
        };
    }

    private static FoodReviewResponse ToFoodPublicResponse(FoodReview review)
        => new()
        {
            Id = review.Id,
            OrderDetailId = review.OrderDetailId,
            OrderId = review.OrderId,
            FoodItemId = review.FoodItemId,
            FoodItemName = review.FoodItem?.Name,
            BoothId = review.BoothId,
            BoothName = review.Booth?.BoothName,
            CustomerId = review.CustomerId,
            CustomerName = review.Customer?.FullName,
            Rating = review.Rating,
            Content = review.Content,
            ImageUrl = review.ImageUrl,
            IsVisible = review.IsVisible,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt
        };

    private static void ValidateRating(short rating)
    {
        if (rating is < 1 or > 5)
            throw AppException.BadRequest("Rating must be between 1 and 5.", "INVALID_REVIEW_RATING");
    }

    private static void NormalizeAndValidateContent(CreateReviewRequest request)
    {
        request.Content = TextHelper.NormalizeOptionalText(request.Content);
        request.ImageUrl = TextHelper.NormalizeOptionalText(request.ImageUrl);
        ValidateTextLengths(request.Content, request.ImageUrl);
    }

    private static void NormalizeAndValidateContent(UpdateReviewRequest request)
    {
        request.Content = TextHelper.NormalizeOptionalText(request.Content);
        request.ImageUrl = TextHelper.NormalizeOptionalText(request.ImageUrl);
        ValidateTextLengths(request.Content, request.ImageUrl);
    }

    private static void NormalizeAndValidateFoodContent(string? content, string? imageUrl)
        => ValidateTextLengths(TextHelper.NormalizeOptionalText(content), TextHelper.NormalizeOptionalText(imageUrl));

    private static void ValidateTextLengths(string? content, string? imageUrl)
    {
        if (content?.Length > 2000)
            throw AppException.BadRequest("Review content cannot exceed 2000 characters.", "REVIEW_CONTENT_TOO_LONG");
        if (imageUrl?.Length > 500)
            throw AppException.BadRequest("Review image URL cannot exceed 500 characters.", "REVIEW_IMAGE_URL_TOO_LONG");
    }
}
