using AutoMapper;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.NightMarkets;

public class NightMarketService : INightMarketService
{
    private readonly IGenericRepository<NightMarket> _markets;
    private readonly IMapper _mapper;
    public NightMarketService(IGenericRepository<NightMarket> markets, IMapper mapper) {
        _markets = markets;
        _mapper = mapper;
    }

    public async Task<ApiResponse<PaginationResp<NightMarketResponse>>> GetAllAsync(PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _markets.GetPagedAsync(null, pagination.Page, pagination.PageSize, m => m.CreatedAt, false);
        return ApiResponse<PaginationResp<NightMarketResponse>>.SuccessResponse(new PaginationResp<NightMarketResponse>
        {
            Items = _mapper.Map<List<NightMarketResponse>>(items), Page = pagination.Page, PageSize = pagination.PageSize, Total = total
        });
    }

    public async Task<ApiResponse<NightMarketResponse>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var market = await _markets.GetByIdAsync(id);
        return market is null ? ApiResponse<NightMarketResponse>.Failure("Night market was not found.")
            : ApiResponse<NightMarketResponse>.SuccessResponse(_mapper.Map<NightMarketResponse>(market));
    }

    public async Task<ApiResponse<NightMarketResponse>> CreateAsync(CreateNightMarketRequest request, CancellationToken cancellationToken = default)
    {
        var market = _mapper.Map<NightMarket>(request);

        market.Id = Guid.NewGuid(); market.Name = market.Name.Trim(); market.Address = market.Address.Trim();
        market.CreatedAt = DateTime.UtcNow; market.UpdatedAt = market.CreatedAt;

        await _markets.AddAsync(market); await _markets.SaveChangesAsync();
        return ApiResponse<NightMarketResponse>.SuccessResponse(_mapper.Map<NightMarketResponse>(market), "Night market created successfully.");
    }

    public async Task<ApiResponse<NightMarketResponse>> UpdateAsync(Guid id, UpdateNightMarketRequest request, CancellationToken cancellationToken = default)
    {
        var market = await _markets.GetByIdAsync(id);
        if (market is null)
            return ApiResponse<NightMarketResponse>.Failure("Night market was not found.");

        _mapper.Map(request, market);
        market.Name = market.Name.Trim(); market.Address = market.Address.Trim(); market.UpdatedAt = DateTime.UtcNow;

        _markets.Update(market); await _markets.SaveChangesAsync();
        return ApiResponse<NightMarketResponse>.SuccessResponse(_mapper.Map<NightMarketResponse>(market), "Night market updated successfully.");
    }
}
