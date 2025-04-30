using Microsoft.AspNetCore.SignalR;
using SecureMessaging.Server.Models;
using Supabase;
using Supabase.Postgrest;
using Supabase.Postgrest.Requests;
using Supabase.Postgrest.Attributes;
using static Supabase.Postgrest.Constants;
using System.Diagnostics;

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
        try
        {
            // Проверка существования чата и отправителя
            await ValidateChatAndSender(chatId, senderId);

            // Создаем сообщение для БД
            var dbMessage = new DataMessage
            {
                Id = Guid.NewGuid(),
                ChatId = chatId,
                SenderId = senderId,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            // Вставляем в БД
            var response = await _supabase.From<DataMessage>().Insert(dbMessage);
            var insertedMessage = response.Models.First();

            // Обновляем время последнего сообщения
            await UpdateLastMessageTime(chatId);

            // Преобразуем в клиентскую модель
            return new Message
            {
                Id = insertedMessage.Id,
                ChatId = insertedMessage.ChatId,
                SenderId = insertedMessage.SenderId,
                Content = insertedMessage.Content,
                CreatedAt = insertedMessage.CreatedAt,
                IsCurrentUser = insertedMessage.SenderId == senderId,
                SenderName = "You" // Временное значение, будет обновлено при получении
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex}");
            throw;
        }
    }

    private async Task ValidateChatAndSender(Guid chatId, Guid senderId)
    {
        var chatExists = await _supabase.From<Chat>()
            .Where(x => x.Id == chatId)
            .Single();
        if (chatExists == null) throw new Exception("Chat not found");

        var senderExists = await _supabase.From<User>()
            .Where(x => x.Id == senderId)
            .Single();
        if (senderExists == null) throw new Exception("Sender not found");
    }

    private async Task UpdateLastMessageTime(Guid chatId)
    {
        await _supabase.From<Chat>()
            .Where(x => x.Id == chatId)
            .Set(x => x.LastMessageAt, DateTime.UtcNow)
            .Update();
    }

    public async Task<List<Message>> GetChatMessages(Guid chatId, Guid currentUserId)
    {
        try
        {
            var response = await _supabase.From<DataMessage>()
                .Filter("chat_id", Operator.Equals, chatId.ToString())
                .Order("created_at", Ordering.Descending)
                .Get();

            return response.Models.Select(dbMessage => new Message
            {
                Id = dbMessage.Id,
                ChatId = dbMessage.ChatId,
                SenderId = dbMessage.SenderId,
                Content = dbMessage.Content,
                CreatedAt = dbMessage.CreatedAt,
                IsCurrentUser = dbMessage.SenderId == currentUserId,
                SenderName = dbMessage.SenderId == currentUserId ? "You" : "Other"
            }).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting messages: {ex}");
            return new List<Message>();
        }
    }

    public async Task<List<Guid>> GetChatParticipants(Guid chatId)
{
    try
    {
        var response = await _supabase
            .From<UserChat>()
            .Select(x => new object[] { x.UserId })
            .Where(x => x.ChatId == chatId)
            .Get();

        return response.Models.Select(x => x.UserId).ToList();
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Error getting chat participants: {ex}");
        throw;
    }
}
}