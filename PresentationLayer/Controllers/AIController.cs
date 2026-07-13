using System.Security.Claims;
using ApplicationLayer.AI.DTOs;
using ApplicationLayer.AI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AIController : ControllerBase
{
    private readonly IAIRecommendationService _service;

    public AIController(IAIRecommendationService service)
    {
        _service = service;
    }

    private Guid? CurrentUserId
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet("home")]
    [AllowAnonymous]
    public async Task<IActionResult> Home(CancellationToken cancellationToken)
        => Ok(await _service.GetHomeAsync(CurrentUserId, cancellationToken));

    [HttpGet("recommendations/me")]
    public async Task<IActionResult> PersonalizedRecommendations(CancellationToken cancellationToken)
        => Ok(await _service.GetPersonalizedRecommendationsAsync(CurrentUserId, cancellationToken));

    [HttpPost("food-discovery")]
    public async Task<IActionResult> FoodDiscovery(FoodDiscoveryRequest request, CancellationToken cancellationToken)
        => Ok(await _service.FoodDiscoveryAsync(CurrentUserId, request, cancellationToken));

    [HttpPost("dining-plan-assistant")]
    public async Task<IActionResult> DiningPlanAssistant(DiningPlanAssistantRequest request, CancellationToken cancellationToken)
        => Ok(await _service.DiningPlanAssistantAsync(CurrentUserId, request, cancellationToken));

    [HttpPost("dining-plan/confirm")]
    public async Task<IActionResult> ConfirmDiningPlan(ConfirmDiningPlanRequest request, CancellationToken cancellationToken)
        => Ok(await _service.ConfirmDiningPlanAsync(CurrentUserId, request, cancellationToken));

    [HttpPost("dining-plan/regenerate")]
    public async Task<IActionResult> RegenerateDiningPlan(RegenerateDiningPlanRequest request, CancellationToken cancellationToken)
        => Ok(await _service.RegenerateDiningPlanAsync(CurrentUserId, request, cancellationToken));

    [HttpPost("feedback")]
    public async Task<IActionResult> Feedback(AIFeedbackRequest request, CancellationToken cancellationToken)
        => Ok(await _service.SubmitFeedbackAsync(CurrentUserId, request, cancellationToken));

    [HttpGet("logs/{logId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetLog(Guid logId, CancellationToken cancellationToken)
        => Ok(await _service.GetLogAsync(logId, cancellationToken));
}
