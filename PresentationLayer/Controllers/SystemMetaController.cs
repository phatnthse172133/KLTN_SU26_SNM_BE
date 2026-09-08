using System.Reflection;
using ApplicationLayer.Helppers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

/// <summary>
/// Safe, anonymous build probe so deployed vs local Backend can be compared without secrets.
/// </summary>
[ApiController]
[Route("api/system")]
public sealed class SystemMetaController : ControllerBase
{
    // Bumped when ComplaintCategory string-name JSON wire support is present on CreateComplaintRequest.
    public const string ComplaintCategoryWireMarker = "complaint-category-string-enum-v1";

    [AllowAnonymous]
    [HttpGet("build")]
    public IActionResult GetBuild()
    {
        var assembly = typeof(SystemMetaController).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var fileVersion = assembly
            .GetCustomAttribute<AssemblyFileVersionAttribute>()
            ?.Version;

        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            service = "PresentationLayer",
            informationalVersion = informational,
            fileVersion,
            complaintCategoryWire = ComplaintCategoryWireMarker,
            complaintCategoryAcceptsPascalCaseNames = true,
            complaintCategoryAcceptsIntegers = true
        }));
    }
}
