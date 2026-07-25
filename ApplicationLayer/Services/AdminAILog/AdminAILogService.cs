using System;
using System.Linq;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.Exceptions;
using DomainLayer.Common;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.AdminAILog
{
    public class AdminAILogService : IAdminAILogService
    {
        private readonly IAIRecommendationLogRepository _logRepository;

        public AdminAILogService(IAIRecommendationLogRepository logRepository)
        {
            _logRepository = logRepository;
        }

        public async Task<PagedResult<AILogDto>> GetPagedLogsAsync(string? search, AIRecommendationType? type, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var pagedResult = await _logRepository.GetPagedLogsAsync(search, type, page, pageSize, cancellationToken);

            var items = pagedResult.Items.Select(x => new AILogDto
            {
                Id = x.Id,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer?.FullName,
                CustomerEmail = x.Customer?.Email,
                NightMarketId = x.NightMarketId,
                NightMarketName = x.NightMarket?.Name,
                RecommendationType = x.RecommendationType,
                InputJson = x.InputJson,
                ParsedIntentJson = x.ParsedIntentJson,
                ResultJson = x.ResultJson,
                SelectedOptionId = x.SelectedOptionId,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }).ToList();

            return new PagedResult<AILogDto>(items, pagedResult.TotalCount);
        }

        public async Task<AILogDto> GetLogByIdAsync(Guid id)
        {
            var log = await _logRepository.GetByIdAsync(id);
            if (log == null)
            {
                throw AppException.NotFound("AI recommendation log was not found.");
            }

            // Optional: Include Customer and NightMarket if needed, but since GetByIdAsync from GenericRepo might not include them,
            // we could either add a custom GetByIdWithDetailsAsync or just return basic log.
            // Let's keep it simple.

            return new AILogDto
            {
                Id = log.Id,
                CustomerId = log.CustomerId,
                NightMarketId = log.NightMarketId,
                RecommendationType = log.RecommendationType,
                InputJson = log.InputJson,
                ParsedIntentJson = log.ParsedIntentJson,
                ResultJson = log.ResultJson,
                SelectedOptionId = log.SelectedOptionId,
                CreatedAt = log.CreatedAt,
                UpdatedAt = log.UpdatedAt
            };
        }
    }
}
