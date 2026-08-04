using System.Security.Claims;
using ApplicationLayer.AI.DTOs;
using ApplicationLayer.AI.Services;
using ApplicationLayer.Helppers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
[EnableRateLimiting("AIApiPolicy")]
public class AIController : ControllerBase
{
    private readonly IAIRecommendationService _service;

    public AIController(IAIRecommendationService service)
    {
        _service = service;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("home")]
    [AllowAnonymous]
    [DisableRateLimiting]
    public async Task<IActionResult> Home(CancellationToken cancellationToken)
        => Ok(await _service.GetHomeAsync(null, cancellationToken));

    [HttpGet("recommendations/me")]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<ApiResponse<FoodDiscoveryResponse>>> PersonalizedRecommendations(CancellationToken cancellationToken)
        => Ok(await _service.GetPersonalizedRecommendationsAsync(CurrentUserId, cancellationToken));

    [HttpPost("food-discovery")]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<ApiResponse<FoodDiscoveryResponse>>> FoodDiscovery(FoodDiscoveryRequest request, CancellationToken cancellationToken)
        => Ok(await _service.FoodDiscoveryAsync(CurrentUserId, request, cancellationToken));

    [HttpPost("dining-plan-assistant")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> DiningPlanAssistant(DiningPlanAssistantRequest request, CancellationToken cancellationToken)
        => Ok(await _service.DiningPlanAssistantAsync(CurrentUserId, request, cancellationToken));

    [HttpPost("dining-plan/confirm")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> ConfirmDiningPlan(ConfirmDiningPlanRequest request, CancellationToken cancellationToken)
        => Ok(await _service.ConfirmDiningPlanAsync(CurrentUserId, request, cancellationToken));

    [HttpPost("dining-plan/regenerate")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> RegenerateDiningPlan(RegenerateDiningPlanRequest request, CancellationToken cancellationToken)
        => Ok(await _service.RegenerateDiningPlanAsync(CurrentUserId, request, cancellationToken));

    [HttpPost("feedback")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Feedback(AIFeedbackRequest request, CancellationToken cancellationToken)
        => Ok(await _service.SubmitFeedbackAsync(CurrentUserId, request, cancellationToken));

    [HttpGet("logs/{logId:guid}")]
    [Authorize(Roles = "Admin")]
    [DisableRateLimiting]
    public async Task<IActionResult> GetLog(Guid logId, CancellationToken cancellationToken)
        => Ok(await _service.GetLogAsync(logId, cancellationToken));
}
