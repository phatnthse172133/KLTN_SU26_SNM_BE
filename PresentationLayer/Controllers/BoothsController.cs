using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Booths;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/booths")]
public class BoothsController : ControllerBase
{
    private readonly IBoothService _service;
    public BoothsController(IBoothService service) => _service = service;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [Authorize(Roles = "BoothOwner")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(
        [FromQuery] PaginationReq pagination,
        CancellationToken cancellationToken)
        => Ok(await _service.GetMyBoothsAsync(CurrentUserId, pagination, cancellationToken));

    [Authorize(Roles = "BoothOwner")]
    [HttpPut("mine/{boothId:guid}")]
    public async Task<IActionResult> UpdateMine(Guid boothId, UpdateMyBoothRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.UpdateMyBoothAsync(CurrentUserId, boothId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetAllAsync(pagination, cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{boothId:guid}")]
    public async Task<IActionResult> UpdateByAdmin(Guid boothId, AdminUpdateBoothRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.UpdateByAdminAsync(boothId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}
