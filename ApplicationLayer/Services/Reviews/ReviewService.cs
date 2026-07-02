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
using System.Text.Json;

namespace ApplicationLayer.Services.Reviews;

public class ReviewService : IReviewService
{
    private readonly IReviewRepository _reviews;
    private readonly IBoothRepository _booths;
    private readonly IOrderRepository _orders;
    private readonly IMapper _mapper;
    private readonly INotificationService _notifications;

    public ReviewService(IReviewRepository reviews, IBoothRepository booths, IOrderRepository orders, IMapper mapper, INotificationService notifications)
    {
        _reviews = reviews;
        _booths = booths;
        _orders = orders;
        _mapper = mapper;
        _notifications = notifications;
    }

    public async Task<ApiResponse<ReviewResponse>> CreateAsync(Guid customerId, CreateReviewRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateOrderForBoothAsync(customerId, request.OrderId, request.BoothId, requireCompletedOrder: true);

        if (await _reviews.ExistsByOrderAsync(request.OrderId))
            throw AppException.Conflict("This order has already been reviewed.");

        var now = DateTime.UtcNow;
        var review = _mapper.Map<Review>(request);
        review.Id = Guid.NewGuid();
        review.CustomerId = customerId;
        review.IsVisible = true;
        review.CreatedAt = now;
        review.UpdatedAt = now;

        await _reviews.AddAsync(review);
        await _reviews.SaveChangesAsync();
        await _reviews.RefreshBoothAverageRatingAsync(request.BoothId);
        var booth = await _booths.GetByIdAsync(request.BoothId);
        if (booth is not null)
        {
            await _notifications.NotifyAsync(new NotificationMessage(
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

        return ApiResponse<ReviewResponse>.SuccessResponse(ToResponse(review), "Review created successfully.");
    }

    public async Task<ApiResponse<PaginationResp<ReviewResponse>>> GetByBoothAsync(Guid boothId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        if (await _booths.GetByIdAsync(boothId) is null)
            throw AppException.NotFound("Booth was not found.");

        var page = await _reviews.GetPagedVisibleByBoothWithReplyAsync(
            boothId, pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<ReviewResponse>>.SuccessResponse(
            _mapper.MapPage<Review, ReviewResponse>(page, pagination));
    }

    public async Task<ApiResponse<PaginationResp<ReviewResponse>>> GetMineAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var page = await _reviews.GetPagedByCustomerWithReplyAsync(
            customerId, pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<ReviewResponse>>.SuccessResponse(
            _mapper.MapPage<Review, ReviewResponse>(page, pagination));
    }

    public async Task<ApiResponse<PaginationResp<ReviewResponse>>> GetAllAsync(PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var page = await _reviews.GetPagedWithReplyAsync(
            pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<ReviewResponse>>.SuccessResponse(
            _mapper.MapPage<Review, ReviewResponse>(page, pagination));
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

        var response = await _reviews.GetWithReplyByIdAsync(review.Id);
        return ApiResponse<ReviewResponse>.SuccessResponse(ToResponse(response!), request.IsVisible ? "Review is now visible." : "Review is now hidden.");
    }

    public async Task<ApiResponse<ReviewResponse>> UpsertReplyAsync(Guid ownerId, Guid reviewId, UpsertReviewReplyRequest request, CancellationToken cancellationToken = default)
    {
        var review = await _reviews.GetByIdAsync(reviewId);
        if (review is null) 
            throw AppException.NotFound("Review was not found.");

        var booth = await _booths.GetByIdAsync(review.BoothId);
        if (booth is null)
            throw AppException.NotFound("Booth was not found.");

        if (booth.BoothOwnerId != ownerId)
            throw AppException.Forbidden("You do not have permission to reply to this review.");

        await _reviews.UpsertReplyAsync(reviewId, ownerId, request.Content.Trim());
        await _reviews.SaveChangesAsync();
        await _notifications.NotifyAsync(new NotificationMessage(
            review.CustomerId,
            NotificationType.ReviewReplied,
            "Booth replied to your review",
            request.Content.Trim(),
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

    private async Task ValidateOrderForBoothAsync(Guid customerId, Guid orderId, Guid boothId, bool requireCompletedOrder)
    {
        if (await _booths.GetByIdAsync(boothId) is null)
            throw AppException.NotFound("Booth was not found.");

        var order = await _orders.GetByCustomerAsync(customerId, orderId);
        if (order is null)
            throw AppException.NotFound("Order was not found.");

        if (requireCompletedOrder && order.Status != OrderStatus.Completed)
            throw AppException.BadRequest("Only completed orders can be reviewed.");

        if (!await _orders.ContainsBoothItemsAsync(orderId, boothId))
            throw AppException.BadRequest("Order does not contain items from this booth.");
    }

    private ReviewResponse ToResponse(Review review)
        => _mapper.Map<ReviewResponse>(review);
}
