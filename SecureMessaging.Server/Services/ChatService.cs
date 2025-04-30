using Microsoft.AspNetCore.SignalR;
using SecureMessaging.Server.Models;
using Supabase;
using Supabase.Postgrest;
using Supabase.Postgrest.Requests;
using Supabase.Postgrest.Attributes;

namespace SecureMessaging.Server.Services;

public class ChatService
{
    private readonly Supabase.Client _supabase;

    public ChatService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public class RpcResult
    {
        public Guid chat_id { get; set; }
    }

    public async Task<Chat> CreatePrivateChat(Guid user1Id, Guid user2Id)
    {
        try
        {
            // Вызываем PostgreSQL функцию через RPC
            var response = await _supabase.Rpc<RpcResult>("find_or_create_private_chat",
                new Dictionary<string, object>
                {
                { "user1_id", user1Id },
                { "user2_id", user2Id }
                });

            // Получаем ID чата
            var chatId = response.chat_id;

            // Получаем полную информацию о чате
            return await _supabase.From<Chat>()
                .Where(x => x.Id == chatId)
                .Single();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in CreatePrivateChat: {ex}");
            throw new Exception("Failed to create private chat", ex);
        }
    }

    public async Task<Chat> GetPrivateChat(Guid user1Id, Guid user2Id)
    {
        try
        {
            var response = await _supabase.Rpc("find_or_create_private_chat",
                new Dictionary<string, object>
                {
                { "user1_id", user1Id },
                { "user2_id", user2Id }
                });

            var chatId = new Guid(response.Content);

            return await _supabase.From<Chat>()
                .Where(x => x.Id == chatId)
                .Single();
        }
        catch
        {
            return null;
        }
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