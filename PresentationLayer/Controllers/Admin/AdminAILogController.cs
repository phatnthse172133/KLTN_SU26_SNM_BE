using System;
using System.Threading.Tasks;
using ApplicationLayer.Services.AdminAILog;
using DomainLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static DomainLayer.Enums.GeneralEnum;

namespace PresentationLayer.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/ai-logs")]
    [Authorize(Roles = "Admin")]
    public class AdminAILogController : ControllerBase
    {
        private readonly IAdminAILogService _adminAILogService;

        public AdminAILogController(IAdminAILogService adminAILogService)
        {
            _adminAILogService = adminAILogService;
        }

        [HttpGet]
        public async Task<IActionResult> GetPagedLogs(
            [FromQuery] string? search,
            [FromQuery] AIRecommendationType? type,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 10,
            CancellationToken cancellationToken = default)
        {
            var result = await _adminAILogService.GetPagedLogsAsync(search, type, page, limit, cancellationToken);
            return Ok(new
            {
                Code = 200,
                Message = "Success",
                Data = new
                {
                    items = result.Items,
                    totalCount = result.TotalCount
                }
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetLogById(Guid id)
        {
            var result = await _adminAILogService.GetLogByIdAsync(id);
            return Ok(new
            {
                Code = 200,
                Message = "Success",
                Data = result
            });
        }
    }
}
