using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;

namespace InfrastructureLayer.Repositories;

public class AIRecommendationLogRepository : GenericRepository<AIRecommendationLog>, IAIRecommendationLogRepository
{
    public AIRecommendationLogRepository(SNMDbContext context) : base(context)
    {
    }
}
