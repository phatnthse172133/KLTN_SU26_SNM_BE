using System.Security.Claims;
using ApplicationLayer.AI.V2.Recommendations;
using ApplicationLayer.AI.V2.Services;
using ApplicationLayer.Helppers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/customer/ai/v2/recommendations")]
[Authorize(Roles = "Customer")]
[EnableRateLimiting("AIRecommendationV2Policy")]
public sealed class AIRecommendationV2Controller(IFoodRecommendationV2Service service) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<FoodRecommendationV2Response>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<FoodRecommendationV2Response>>> Recommend(CreateFoodRecommendationV2Request request, CancellationToken cancellationToken)
        => Ok(await service.RecommendAsync(CurrentUserId, request, cancellationToken));

    [HttpPost("{sessionId:guid}/feedback")]
    [ProducesResponseType(typeof(ApiResponse<RecommendationFeedbackV2Response>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<ErrorResponse>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<RecommendationFeedbackV2Response>>> Feedback(Guid sessionId, RecommendationFeedbackV2Request request, CancellationToken cancellationToken)
        => Ok(await service.FeedbackAsync(CurrentUserId, sessionId, request, cancellationToken));
}
