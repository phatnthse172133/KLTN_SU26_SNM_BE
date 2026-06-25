using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Complaints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/complaints")]
public class ComplaintsController : ControllerBase
{
    private readonly IComplaintService _service;
    public ComplaintsController(IComplaintService service) => _service = service;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [Authorize(Roles = "Customer")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateComplaintRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.CreateAsync(CurrentUserId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [Authorize(Roles = "Customer")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] PaginationReq pagination, CancellationToken cancellationToken)
        => Ok(await _service.GetMineAsync(CurrentUserId, pagination, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationReq pagination, CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(pagination, cancellationToken));

    [Authorize(Roles = "BoothOwner")]
    [HttpGet("booths/{boothId:guid}")]
    public async Task<IActionResult> GetByBooth(Guid boothId, [FromQuery] PaginationReq pagination, CancellationToken cancellationToken)
        => Ok(await _service.GetByBoothAsync(CurrentUserId, boothId, pagination, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPut("{complaintId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid complaintId, UpdateComplaintStatusRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.UpdateStatusAsync(complaintId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}
