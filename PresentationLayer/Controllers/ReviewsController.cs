using System.Security.Claims;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _service;
    public ReviewsController(IReviewService service) => _service = service;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [Authorize(Roles = "Customer")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateReviewRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.CreateAsync(CurrentUserId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [AllowAnonymous]
    [HttpGet("booths/{boothId:guid}")]
    public async Task<IActionResult> GetByBooth(Guid boothId, [FromQuery] PaginationReq pagination, CancellationToken cancellationToken)
        => Ok(await _service.GetByBoothAsync(boothId, pagination, cancellationToken));

    [Authorize(Roles = "Customer")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] PaginationReq pagination, CancellationToken cancellationToken)
        => Ok(await _service.GetMineAsync(CurrentUserId, pagination, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationReq pagination, CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(pagination, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPatch("{reviewId:guid}/visibility")]
    public async Task<IActionResult> UpdateVisibility(Guid reviewId, UpdateReviewVisibilityRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.UpdateVisibilityAsync(reviewId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [Authorize(Roles = "BoothOwner")]
    [HttpPut("{reviewId:guid}/reply")]
    public async Task<IActionResult> UpsertReply(Guid reviewId, UpsertReviewReplyRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.UpsertReplyAsync(CurrentUserId, reviewId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}
