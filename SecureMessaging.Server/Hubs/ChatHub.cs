﻿using Microsoft.AspNetCore.Authorization;
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
    private readonly AuthService _authService;
    private readonly ILogger<ChatHub> _logger;
    private readonly Supabase.Client _supabase;

    public ChatHub(ChatService chatService, UserService userService, AuthService authService, ILogger<ChatHub> logger, Supabase.Client supabase)
    {
        _chatService = chatService;
        _userService = userService;
        _authService = authService;
        _logger = logger;
        _supabase = supabase;
    }

    public async Task<List<Message>> GetChatMessages(Guid chatId)
    {
        var userId = _authService.GetCurrentUserId(Context.User);
        return await _chatService.GetChatMessages(chatId, userId);
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

        // Get chat participants and sender info
        var chatParticipants = await _chatService.GetChatParticipants(chatId);
        var sender = await _userService.GetUserById(userId);

        // Prepare message for each participant
        foreach (var participant in chatParticipants)
        {
            var clientMessage = new Message
            {
                Id = message.Id,
                ChatId = message.ChatId,
                SenderId = message.SenderId,
                Content = message.Content,
                CreatedAt = message.CreatedAt,
                IsCurrentUser = participant == userId,
                SenderName = participant == userId
                    ? "You"
                    : sender?.DisplayName ?? sender?.Username ?? "Unknown"
            };

            await Clients.Group(participant.ToString()).SendAsync("ReceiveMessage", clientMessage);
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

    public async Task<List<ChatDto>> GetUserChats()
    {
        try
        {
            var userId = GetUserId();
            return await _chatService.GetUserChats(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user chats");
            throw new HubException("Failed to get user chats");
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

    public async Task<bool> IsPrimaryDevice(Guid userId, string deviceName)
    {
        try
        {
            var device = await _supabase.From<Device>()
                .Where(d => d.UserId == userId && d.DeviceName == deviceName)
                .Single();

            return device?.IsPrimary ?? false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsRestrictedUser(Guid userId)
    {
        try
        {
            var user = await _supabase.From<User>()
                .Where(u => u.Id == userId)
                .Single();

            return user?.IsRestricted ?? false;
        }
        catch
        {
            return true; // Если не удалось проверить, считаем что ограничен
        }
    }
}