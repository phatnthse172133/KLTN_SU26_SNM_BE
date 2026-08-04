using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public sealed class CustomerOrderHistoryRequest : PaginationReq
{
    public OrderStatus? Status { get; set; }
}
