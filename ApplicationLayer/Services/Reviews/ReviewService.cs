using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Subscriptions;
using System.Text.Json;

namespace ApplicationLayer.Services.Reviews;

public class ReviewService : IReviewService
{
    private readonly IReviewRepository _reviews;
    private readonly IBoothRepository _booths;
    private readonly IOrderRepository _orders;
    private readonly IMapper _mapper;
    private readonly INotificationService _notifications;
    private readonly ISubscriptionEntitlementService _entitlements;

    public ReviewService(
        IReviewRepository reviews,
        IBoothRepository booths,
        IOrderRepository orders,
        IMapper mapper,
        INotificationService notifications,
        ISubscriptionEntitlementService entitlements)
    {
        _reviews = reviews;
        _booths = booths;
        _orders = orders;
        _mapper = mapper;
        _notifications = notifications;
        _entitlements = entitlements;
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

    private static CustomerReviewHistoryResponse ToCustomerResponse(Review review)
        => new()
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

    private static void ValidateTextLengths(string? content, string? imageUrl)
    {
        if (content?.Length > 2000)
            throw AppException.BadRequest("Review content cannot exceed 2000 characters.", "REVIEW_CONTENT_TOO_LONG");
        if (imageUrl?.Length > 500)
            throw AppException.BadRequest("Review image URL cannot exceed 500 characters.", "REVIEW_IMAGE_URL_TOO_LONG");
    }
}
