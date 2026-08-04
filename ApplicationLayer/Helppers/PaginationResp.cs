namespace ApplicationLayer.Helppers
{
    public class PaginationResp<T>
    {
        public IReadOnlyCollection<T> Items { get; init; } = [];
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int Total { get; init; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

        public static PaginationResp<T> Create(
            IReadOnlyCollection<T> items,
            int totalCount,
            PaginationReq request)
            => new()
            {
                Items = items,
                Page = request.Page,
                PageSize = request.PageSize,
                Total = totalCount
            };
    }
}
