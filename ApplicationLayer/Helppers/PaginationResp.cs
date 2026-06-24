namespace ApplicationLayer.Helppers
{
    public class PaginationResp<T>
    {
        public IReadOnlyCollection<T> Items { get; init; } = [];
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public int TotalPages => (int)Math.Ceiling(Total / (double)PageSize);
    }
}
