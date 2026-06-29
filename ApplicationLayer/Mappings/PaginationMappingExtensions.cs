using ApplicationLayer.Helppers;
using AutoMapper;
using DomainLayer.Common;

namespace ApplicationLayer.Mappings;

public static class PaginationMappingExtensions
{
    public static PaginationResp<TDestination> MapPage<TSource, TDestination>(
        this IMapper mapper,
        PagedResult<TSource> source,
        PaginationReq request)
        => PaginationResp<TDestination>.Create(
            mapper.Map<List<TDestination>>(source.Items),
            source.TotalCount,
            request);
}
