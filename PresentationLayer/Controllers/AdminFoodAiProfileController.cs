using ApplicationLayer.Services.Menus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[Route("api/admin/food-ai-profiles")]
[ApiController]
[Authorize(Roles = "Admin")]
public sealed class AdminFoodAiProfileController(IFoodAiProfileEnrichmentService enrichment) : ControllerBase
{
    public sealed class FoodAiProfileBackfillRequest
    {
        public int? BatchSize { get; set; }
        public bool RetryFailed { get; set; }
    }

    [HttpPost("backfill")]
    public async Task<IActionResult> Backfill([FromBody] FoodAiProfileBackfillRequest? request, CancellationToken cancellationToken)
    {
        var batchSize = Math.Clamp(request?.BatchSize ?? 20, 1, 50);
        var result = await enrichment.BackfillAsync(batchSize, request?.RetryFailed ?? false, cancellationToken);
        return Ok(new
        {
            processed = result.Processed,
            succeeded = result.Succeeded,
            failed = result.Failed,
            skipped = result.Skipped,
            batchSize,
            retryFailed = request?.RetryFailed ?? false
        });
    }

    [HttpPost("{foodItemId:guid}/regenerate")]
    public async Task<IActionResult> Regenerate(Guid foodItemId, CancellationToken cancellationToken)
    {
        await enrichment.EnrichOneAsync(foodItemId, force: true, cancellationToken);
        return Ok(new { foodItemId, accepted = true });
    }
}
