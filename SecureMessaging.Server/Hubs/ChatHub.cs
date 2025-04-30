using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SecureMessaging.Server.Models;
using SecureMessaging.Server.Services;
using System.Security.Claims;

namespace SecureMessaging.Server.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ChatService _chatService;
    private readonly UserService _userService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ChatService chatService, UserService userService, ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _userService = userService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, userId.ToString());
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var userId = GetUserId();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId.ToString());
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(Guid chatId, string content)
    {
        var userId = GetUserId();
        var message = await _chatService.SendMessage(chatId, userId, content);

        // Get chat participants
        var chatParticipants = await _chatService.GetChatParticipants(chatId);

        // Log message to console
        _logger.LogInformation($"Message sent to chat {chatId} by user {userId}: {content}");

        // Send message to all participants
        foreach (var participant in chatParticipants)
        {
            await Clients.Group(participant.ToString()).SendAsync("ReceiveMessage", message);
        }
    }

    public async Task StartPrivateChat(Guid otherUserId)
    {
        var userId = GetUserId();

        try
        {
            _logger.LogInformation($"Creating private chat between {userId} and {otherUserId}");

            var chat = await _chatService.CreatePrivateChat(userId, otherUserId);

            _logger.LogInformation($"Chat created: {chat.Id}");

            var otherUser = await _userService.GetUserById(otherUserId);
            var currentUser = await _userService.GetUserById(userId);

            // Отправляем информацию о чате обоим пользователям
            await Clients.Caller.SendAsync("ChatStarted", new
            {
                chat.Id,
                chat.IsGroup,
                chat.CreatedAt,
                chat.LastMessageAt,
                DisplayName = otherUser?.DisplayName ?? otherUser?.Username ?? "Unknown"
            });

            await Clients.User(otherUserId.ToString()).SendAsync("ChatStarted", new
            {
                chat.Id,
                chat.IsGroup,
                chat.CreatedAt,
                chat.LastMessageAt,
                DisplayName = currentUser?.DisplayName ?? currentUser?.Username ?? "Unknown"
            });
            _logger.LogInformation("Chat open");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating private chat");
            throw new HubException("Failed to create private chat");
        }
    }



    private Guid GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? Context.User?.FindFirst("id")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new HubException("Invalid user ID");
        }

        return userId;
    }
}