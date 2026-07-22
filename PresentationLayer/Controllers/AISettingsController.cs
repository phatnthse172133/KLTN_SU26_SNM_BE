using ApplicationLayer.AI.Services;
using ApplicationLayer.DTOs.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/admin/ai-settings")]
[Authorize(Roles = "Admin")]
public class AISettingsController : ControllerBase
{
    private readonly IAISettingsService _service;

    public AISettingsController(IAISettingsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
        => Ok(await _service.GetAsync(cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Update(UpdateAISettingsRequest request, CancellationToken cancellationToken = default)
        => Ok(await _service.UpdateAsync(request, cancellationToken));
}
