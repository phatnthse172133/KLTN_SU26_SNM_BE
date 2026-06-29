namespace DomainLayer.Common;

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int TotalCount);
