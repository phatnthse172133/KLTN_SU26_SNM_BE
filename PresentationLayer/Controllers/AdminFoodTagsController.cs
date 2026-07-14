using ApplicationLayer.AI.DTOs;
using ApplicationLayer.AI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/admin/food-tags")]
[Authorize(Roles = "Admin")]
public class AdminFoodTagsController : ControllerBase
{
    private readonly IFoodTagService _service;

    public AdminFoodTagsController(IFoodTagService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateFoodTagRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPut("{tagId:guid}")]
    public async Task<IActionResult> Update(Guid tagId, UpdateFoodTagRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(tagId, request, cancellationToken));

    [HttpDelete("{tagId:guid}")]
    public async Task<IActionResult> Delete(Guid tagId, CancellationToken cancellationToken)
        => Ok(await _service.DeleteAsync(tagId, cancellationToken));
}
