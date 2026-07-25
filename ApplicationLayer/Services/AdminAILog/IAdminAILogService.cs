using System;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Admin;
using DomainLayer.Common;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.AdminAILog
{
    public interface IAdminAILogService
    {
        Task<PagedResult<AILogDto>> GetPagedLogsAsync(string? search, AIRecommendationType? type, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<AILogDto> GetLogByIdAsync(Guid id);
    }
}
