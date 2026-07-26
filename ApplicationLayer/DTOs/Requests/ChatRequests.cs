using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class ChatListRequest : PaginationReq
{
}

public class MessageListRequest : PaginationReq
{
}

public class CreateCustomerBoothConversationRequest
{
    [Required]
    public Guid BoothId { get; set; }
}

public class SendMessageRequest
{
    public Guid? ClientMessageId { get; set; }

    public MessageType Type { get; set; } = MessageType.Text;

    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;
}
