using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.Chats;
using ApplicationLayer.Services.Notifications;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class ChatServiceTests
{
    private readonly Mock<IConversationRepository> _conversations = new();
    private readonly Mock<IMessageRepository> _messages = new();
    private readonly Mock<IBoothRepository> _booths = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IRealtimeChatPublisher> _realtime = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly ChatService _service;

    public ChatServiceTests()
    {
        _service = new ChatService(
            _conversations.Object,
            _messages.Object,
            _booths.Object,
            _mapper.Object,
            _realtime.Object,
            _notifications.Object,
            Mock.Of<ILogger<ChatService>>());
    }

    [Fact]
    public async Task CreateConversation_UsesCustomerAndBoothAsIdempotencyKey()
    {
        var customerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        var booth = new Booth { Id = boothId, BoothOwnerId = ownerId };
        var conversation = Conversation(customerId, ownerId, booth);

        _booths.Setup(repository => repository.CustomerVisibleExistsAsync(boothId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _booths.Setup(repository => repository.GetByIdAsync(boothId)).ReturnsAsync(booth);
        _conversations.Setup(repository => repository.GetOrCreateCustomerBoothAsync(
                customerId,
                boothId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((conversation, false));
        _messages.Setup(repository => repository.CountUnreadAsync(
                conversation.Id,
                customerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _mapper.Setup(mapper => mapper.Map<ConversationResponse>(conversation))
            .Returns(new ConversationResponse { Id = conversation.Id, BoothId = boothId });

        var result = await _service.CreateCustomerBoothConversationAsync(
            customerId,
            new CreateCustomerBoothConversationRequest { BoothId = boothId });

        Assert.True(result.Success);
        Assert.Equal("Conversation already exists.", result.Message);
        _conversations.Verify(repository => repository.GetOrCreateCustomerBoothAsync(
            customerId,
            boothId,
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetConversation_ForNonParticipant_ReturnsHiddenNotFound()
    {
        _conversations.Setup(repository => repository.GetOwnedWithUsersAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _service.GetConversationAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal("CHAT_CONVERSATION_NOT_FOUND", exception.ErrorCode);
    }

    [Fact]
    public async Task SendMessage_RejectsUnsupportedClientMessageType()
    {
        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _service.SendMessageAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new SendMessageRequest { Type = MessageType.Image, Content = "https://example.test/image" }));

        Assert.Equal("CHAT_MESSAGE_TYPE_UNSUPPORTED", exception.ErrorCode);
        _messages.Verify(repository => repository.AddAsync(It.IsAny<Message>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_ReusedClientIdWithDifferentPayload_ReturnsConflict()
    {
        var customerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var clientMessageId = Guid.NewGuid();
        var booth = new Booth { Id = Guid.NewGuid(), BoothOwnerId = Guid.NewGuid() };
        var conversation = Conversation(customerId, booth.BoothOwnerId, booth, conversationId);
        var existing = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = customerId,
            ClientMessageId = clientMessageId,
            Type = MessageType.Text,
            Content = "original"
        };

        _conversations.Setup(repository => repository.GetOwnedWithUsersAsync(
                conversationId,
                customerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
        _messages.Setup(repository => repository.GetByClientMessageIdAsync(
                customerId,
                clientMessageId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _service.SendMessageAsync(
                customerId,
                conversationId,
                new SendMessageRequest
                {
                    ClientMessageId = clientMessageId,
                    Type = MessageType.Text,
                    Content = "different"
                }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("CHAT_CLIENT_MESSAGE_ID_PAYLOAD_CONFLICT", exception.ErrorCode);
    }

    [Fact]
    public async Task SendMessage_SameClientIdAndPayload_ReturnsExistingMessageWithoutInsert()
    {
        var customerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var clientMessageId = Guid.NewGuid();
        var booth = new Booth { Id = Guid.NewGuid(), BoothOwnerId = Guid.NewGuid() };
        var conversation = Conversation(customerId, booth.BoothOwnerId, booth, conversationId);
        var existing = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = customerId,
            ClientMessageId = clientMessageId,
            Type = MessageType.Text,
            Content = "hello"
        };
        var mapped = new MessageResponse { Id = existing.Id, Content = existing.Content };

        _conversations.Setup(repository => repository.GetOwnedWithUsersAsync(
                conversationId,
                customerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
        _messages.Setup(repository => repository.GetByClientMessageIdAsync(
                customerId,
                clientMessageId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _mapper.Setup(mapper => mapper.Map<MessageResponse>(existing)).Returns(mapped);

        var result = await _service.SendMessageAsync(
            customerId,
            conversationId,
            new SendMessageRequest
            {
                ClientMessageId = clientMessageId,
                Type = MessageType.Text,
                Content = " hello "
            });

        Assert.True(result.Success);
        Assert.Equal(existing.Id, result.Data!.Id);
        Assert.Equal("Message already exists.", result.Message);
        _messages.Verify(repository => repository.AddAsync(It.IsAny<Message>()), Times.Never);
        _notifications.Verify(service => service.NotifyAsync(
            It.IsAny<NotificationMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_PostCommitDeliveryFailures_DoNotFailPersistedMessage()
    {
        var customerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var booth = new Booth { Id = Guid.NewGuid(), BoothOwnerId = ownerId };
        var conversation = Conversation(customerId, ownerId, booth, conversationId);
        Message? persisted = null;

        _conversations.Setup(repository => repository.GetOwnedWithUsersAsync(
                conversationId,
                customerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
        _booths.Setup(repository => repository.CustomerVisibleExistsAsync(
                booth.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _messages.Setup(repository => repository.AddAsync(It.IsAny<Message>()))
            .Callback<Message>(message => persisted = message)
            .Returns(Task.CompletedTask);
        _messages.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);
        _messages.Setup(repository => repository.GetOwnedAsync(
                It.IsAny<Guid>(),
                customerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => persisted);
        _mapper.Setup(mapper => mapper.Map<MessageResponse>(It.IsAny<Message>()))
            .Returns((Message message) => new MessageResponse
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderName = "Customer",
                Type = message.Type,
                Content = message.Content,
                CreatedAt = message.CreatedAt
            });
        _realtime.Setup(publisher => publisher.PublishMessageCreatedAsync(
                conversationId,
                It.IsAny<MessageResponse>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));
        _notifications.Setup(service => service.NotifyAsync(
                It.IsAny<NotificationMessage>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notification unavailable"));

        var result = await _service.SendMessageAsync(
            customerId,
            conversationId,
            new SendMessageRequest { Type = MessageType.Text, Content = "  hello  " });

        Assert.True(result.Success);
        Assert.Equal("hello", persisted!.Content);
        _messages.Verify(repository => repository.SaveChangesAsync(), Times.Once);
        _notifications.Verify(service => service.NotifyAsync(
            It.Is<NotificationMessage>(message =>
                message.UserId == ownerId
                && message.Type == NotificationType.NewMessage
                && message.BoothId == booth.Id
                && message.ReferenceType == "Conversation"
                && message.ReferenceId == conversationId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkRead_MarksOnlyIncomingMessagesAndConversationNotifications()
    {
        var customerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var booth = new Booth { Id = Guid.NewGuid(), BoothOwnerId = Guid.NewGuid() };
        var conversation = Conversation(customerId, booth.BoothOwnerId, booth, conversationId);

        _conversations.Setup(repository => repository.GetOwnedWithUsersAsync(
                conversationId,
                customerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
        _messages.Setup(repository => repository.MarkConversationMessagesReadAsync(
                conversationId,
                customerId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _conversations.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _service.MarkReadAsync(customerId, conversationId);

        Assert.True(result.Success);
        Assert.NotNull(conversation.CustomerLastReadAt);
        Assert.Null(conversation.BoothOwnerLastReadAt);
        _messages.Verify(repository => repository.MarkConversationMessagesReadAsync(
            conversationId,
            customerId,
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(service => service.MarkReferenceReadAsync(
            customerId,
            "Conversation",
            conversationId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Conversation Conversation(
        Guid customerId,
        Guid ownerId,
        Booth booth,
        Guid? conversationId = null)
    {
        booth.BoothOwner = new User { Id = ownerId, FullName = "Owner" };
        return new Conversation
        {
            Id = conversationId ?? Guid.NewGuid(),
            CustomerId = customerId,
            Customer = new User { Id = customerId, FullName = "Customer" },
            BoothId = booth.Id,
            Booth = booth,
            Status = ConversationStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
