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

    /// <summary>
    /// Overload that accepts pre-mapped items (useful when manual enrichment is needed per-item).
    /// </summary>
    public static PaginationResp<TDestination> MapPage<TSource, TDestination>(
        this IMapper mapper,
        PagedResult<TSource> source,
        PaginationReq request,
        IEnumerable<TDestination> mappedItems)
        => PaginationResp<TDestination>.Create(
            mappedItems.ToList(),
            source.TotalCount,
            request);
}
