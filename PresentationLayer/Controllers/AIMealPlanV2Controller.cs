using System.Security.Claims;
using ApplicationLayer.AI.V2.MealPlans;
using ApplicationLayer.AI.V2.Services;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/customer/ai/v2/meal-plans")]
[Authorize(Roles = "Customer")]
public sealed class AIMealPlanV2Controller(IMealPlanV2Service service) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    [EnableRateLimiting("AIMealPlanCreateV2Policy")]
    [ProducesResponseType(typeof(ApiResponse<MealPlanV2Response>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<MealPlanV2Response>>> Create(CreateMealPlanV2Request request, CancellationToken cancellationToken)
        => Ok(await service.CreateAsync(CurrentUserId, request, cancellationToken));

    [HttpGet("{planId:guid}")]
    [EnableRateLimiting("AIMealPlanReadV2Policy")]
    [ProducesResponseType(typeof(ApiResponse<MealPlanDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<MealPlanDetailResponse>>> Detail(Guid planId, CancellationToken cancellationToken)
        => Ok(await service.GetDetailAsync(CurrentUserId, planId, cancellationToken));

    [HttpPost("{planId:guid}/add-to-cart")]
    [EnableRateLimiting("AIMealPlanMutationV2Policy")]
    [ProducesResponseType(typeof(ApiResponse<MealPlanAddToCartResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<MealPlanAddToCartResponse>>> AddToCart(
        Guid planId, AddMealPlanToCartRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddToCartAsync(CurrentUserId, planId, request, cancellationToken));

    [HttpPost("{planId:guid}/refresh-prices")]
    [EnableRateLimiting("AIMealPlanMutationV2Policy")]
    [ProducesResponseType(typeof(ApiResponse<MealPlanDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<MealPlanDetailResponse>>> RefreshPrices(
        Guid planId, RefreshMealPlanPricesRequest request, CancellationToken cancellationToken)
        => Ok(await service.RefreshPricesAsync(CurrentUserId, planId, request, cancellationToken));

    [HttpGet("{planId:guid}/items/{itemId:guid}/alternatives")]
    [EnableRateLimiting("AIMealPlanReadV2Policy")]
    [ProducesResponseType(typeof(ApiResponse<MealPlanAlternativePageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<MealPlanAlternativePageResponse>>> Alternatives(Guid planId, Guid itemId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
        => Ok(await service.GetAlternativesAsync(CurrentUserId, planId, itemId, page, pageSize, cancellationToken));

    [HttpPut("{planId:guid}/items/{itemId:guid}")]
    [EnableRateLimiting("AIMealPlanMutationV2Policy")]
    [ProducesResponseType(typeof(ApiResponse<MealPlanDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<MealPlanDetailResponse>>> Replace(Guid planId, Guid itemId,
        ReplaceMealPlanItemRequest request, CancellationToken cancellationToken)
        => Ok(await service.ReplaceAsync(CurrentUserId, planId, itemId, request, cancellationToken));

    [HttpDelete("{planId:guid}/items/{itemId:guid}")]
    [EnableRateLimiting("AIMealPlanMutationV2Policy")]
    [ProducesResponseType(typeof(ApiResponse<MealPlanDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<MealPlanDetailResponse>>> Remove(Guid planId, Guid itemId,
        [FromQuery] int expectedPlanVersion, CancellationToken cancellationToken)
        => Ok(await service.RemoveAsync(CurrentUserId, planId, itemId, expectedPlanVersion, cancellationToken));

    [HttpPost("{planId:guid}/courses/{course}/regenerate")]
    [EnableRateLimiting("AIMealPlanMutationV2Policy")]
    [ProducesResponseType(typeof(ApiResponse<MealPlanDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<MealPlanDetailResponse>>> Regenerate(Guid planId, string course,
        RegenerateMealPlanCourseRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<FoodCourse>(course, false, out var parsed))
            throw AppException.BadRequest("Course is invalid.", "AI_COURSE_NOT_SUPPORTED");
        return Ok(await service.RegenerateCourseAsync(CurrentUserId, planId, parsed, request, cancellationToken));
    }
}
