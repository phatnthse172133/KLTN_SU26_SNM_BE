using DomainLayer.Common;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository;

public interface IAIRecommendationLogRepository : IGenericRepository<AIRecommendationLog>
{
    Task<PagedResult<AIRecommendationLog>> GetPagedLogsAsync(
        string? search,
        AIRecommendationType? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
