using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Complaints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static DomainLayer.Enums.GeneralEnum;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/complaints")]
public class ComplaintsController : ControllerBase
{
    private readonly IComplaintService _service;
    public ComplaintsController(IComplaintService service)
    {
        _service = service;
    }

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

    [Authorize(Roles = "Customer")]
    [HttpGet("mine/{complaintId:guid}")]
    public async Task<IActionResult> GetMineDetail(Guid complaintId, CancellationToken cancellationToken)
        => Ok(await _service.GetMineDetailAsync(CurrentUserId, complaintId, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] AdminComplaintQueryRequest query, CancellationToken cancellationToken)
        => Ok(await _service.GetAllFilteredAsync(query, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpGet("counts")]
    public async Task<IActionResult> GetCounts(CancellationToken cancellationToken)
        => Ok(await _service.GetCountsAsync(cancellationToken));

    [Authorize(Roles = "BoothOwner")]
    [HttpGet("booths/{boothId:guid}")]
    public async Task<IActionResult> GetByBooth(Guid boothId, [FromQuery] PaginationReq pagination, CancellationToken cancellationToken)
        => Ok(await _service.GetByBoothAsync(CurrentUserId, boothId, pagination, cancellationToken));

    [Authorize(Roles = "MarketOwner")]
    [HttpGet("market-owner")]
    public async Task<IActionResult> GetByMarketOwner([FromQuery] MarketOwnerComplaintQueryRequest query, CancellationToken cancellationToken)
        => Ok(await _service.GetByMarketOwnerAsync(CurrentUserId, query, cancellationToken));

    [Authorize(Roles = "MarketOwner")]
    [HttpGet("market-owner/{complaintId:guid}")]
    public async Task<IActionResult> GetDetailForMarketOwner(Guid complaintId, CancellationToken cancellationToken)
        => Ok(await _service.GetDetailForMarketOwnerAsync(CurrentUserId, complaintId, cancellationToken));

    [Authorize(Roles = "MarketOwner")]
    [HttpGet("market-owner/counts")]
    public async Task<IActionResult> GetCountsByMarketOwner(CancellationToken cancellationToken)
        => Ok(await _service.GetCountsByMarketOwnerAsync(CurrentUserId, cancellationToken));

    [Authorize(Roles = "MarketOwner,Admin")]
    [HttpPut("{complaintId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid complaintId, UpdateComplaintStatusRequest request, CancellationToken cancellationToken)
    {
        var actorId = User.IsInRole("MarketOwner") ? CurrentUserId : (Guid?)null;
        var response = await _service.UpdateStatusAsync(complaintId, request, cancellationToken, actorId);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}
