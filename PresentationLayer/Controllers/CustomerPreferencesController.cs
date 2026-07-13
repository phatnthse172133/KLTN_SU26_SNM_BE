using System.Security.Claims;
using ApplicationLayer.AI.DTOs;
using ApplicationLayer.AI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/customers/me/preferences")]
[Authorize(Roles = "Customer")]
public class CustomerPreferencesController : ControllerBase
{
    private readonly ICustomerPreferenceService _service;

    public CustomerPreferencesController(ICustomerPreferenceService service)
    {
        _service = service;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await _service.GetMineAsync(CurrentUserId, cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Update(UpdateCustomerPreferenceRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateMineAsync(CurrentUserId, request, cancellationToken));
}
