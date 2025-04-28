using SecureMessaging.Server.Models;
using Supabase;

namespace SecureMessaging.Server.Services;

public class ChatService
{
    private readonly Supabase.Client _supabase;

    public ChatService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<Chat> CreatePrivateChat(Guid user1Id, Guid user2Id)
    {
        // Check if chat already exists
        var existingChat = await GetPrivateChat(user1Id, user2Id);
        if (existingChat != null)
        {
            return existingChat;
        }

        // Create new chat
        var newChat = new Chat
        {
            Id = Guid.NewGuid(), // Явно задаем ID
            IsGroup = false,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = null
        };

        var chatResponse = await _supabase
            .From<Chat>()
            .Insert(newChat);

        var createdChat = chatResponse.Models.First();

        // Add users to chat
        await _supabase
            .From<UserChat>()
            .Insert(new UserChat { UserId = user1Id, ChatId = createdChat.Id });

        await _supabase
            .From<UserChat>()
            .Insert(new UserChat { UserId = user2Id, ChatId = createdChat.Id });

        return createdChat;
    }


    public async Task<Chat> GetPrivateChat(Guid user1Id, Guid user2Id)
    {
        // Get all chats for user1
        var user1Chats = await _supabase
            .From<UserChat>()
            .Where(x => x.UserId == user1Id)
            .Get();

        // Get all chats for user2
        var user2Chats = await _supabase
            .From<UserChat>()
            .Where(x => x.UserId == user2Id)
            .Get();

        // Find intersection (chats both users are in)
        var commonChatIds = user1Chats.Models.Select(x => x.ChatId)
            .Intersect(user2Chats.Models.Select(x => x.ChatId));

        // Check if any of these chats are private (non-group)
        foreach (var chatId in commonChatIds)
        {
            var chat = await _supabase
                .From<Chat>()
                .Where(x => x.Id == chatId && !x.IsGroup)
                .Single();

            if (chat != null)
            {
                return chat;
            }
        }

        return null;
    }

    public async Task<List<Chat>> GetUserChats(Guid userId)
    {
        var userChats = await _supabase
            .From<UserChat>()
            .Where(x => x.UserId == userId)
            .Get();

        var chatIds = userChats.Models.Select(x => x.ChatId).ToList();

        if (chatIds.Count == 0)
        {
            return new List<Chat>();
        }

        var chatsResponse = await _supabase
            .From<Chat>()
            .Where(x => chatIds.Contains(x.Id))
            .Order(x => x.LastMessageAt, Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        return chatsResponse.Models;
    }

    public async Task<Message> SendMessage(Guid chatId, Guid senderId, string content)
    {
        var newMessage = new Message
        {
            ChatId = chatId,
            SenderId = senderId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        var response = await _supabase
            .From<Message>()
            .Insert(newMessage);

        var createdMessage = response.Models.First();

        // Update chat's last message timestamp
        var chat = await _supabase
            .From<Chat>()
            .Where(x => x.Id == chatId)
            .Single();

        if (chat != null)
        {
            chat.LastMessageAt = DateTime.UtcNow;
            await _supabase.From<Chat>().Update(chat);
        }

        return createdMessage;
    }

    public async Task<List<Message>> GetChatMessages(Guid chatId)
    {
        var response = await _supabase
            .From<Message>()
            .Where(x => x.ChatId == chatId)
            .Order(x => x.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models;
    }

    public async Task<List<Guid>> GetChatParticipants(Guid chatId)
    {
        var response = await _supabase
            .From<UserChat>()
            .Where(x => x.ChatId == chatId)
            .Get();

        return response.Models.Select(x => x.UserId).ToList();
    }
}