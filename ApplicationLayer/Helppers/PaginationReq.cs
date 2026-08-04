using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.Helppers
{
    public class PaginationReq
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 10;
    }

    public class UserListQuery : PaginationReq
    {
        public string? Keyword { get; set; }
        public string? Role { get; set; }
        public string? Status { get; set; }
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; }
    }
}
