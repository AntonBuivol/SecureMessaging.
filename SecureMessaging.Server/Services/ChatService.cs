using Microsoft.AspNetCore.SignalR;
using SecureMessaging.Server.Models;
using Supabase;
using Supabase.Postgrest;
using Supabase.Postgrest.Requests;
using Supabase.Postgrest.Attributes;
using static Supabase.Postgrest.Constants;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

    public async Task<List<ChatDto>> GetUserChats(Guid userId)
    {
        try
        {
            // Получаем все chat_id для пользователя
            var userChatsResponse = await _supabase
                .From<UserChat>()
                .Select(x => new object[] { x.ChatId })
                .Where(x => x.UserId == userId)
                .Get();

            if (userChatsResponse.Models.Count == 0)
                return new List<ChatDto>();

            // Преобразуем GUID в строки для фильтрации
            var chatIdStrings = userChatsResponse.Models
                .Select(x => x.ChatId.ToString())
                .ToList();

            // Получаем информацию о чатах
            var chatsResponse = await _supabase
                .From<Chat>()
                .Filter("id", Operator.In, chatIdStrings)
                .Order("last_message_at", Ordering.Descending)
                .Get();

            var chats = new List<ChatDto>();

            // Для каждого чата получаем информацию об участниках
            foreach (var chat in chatsResponse.Models)
            {
                var participants = await GetChatParticipants(chat.Id);
                var otherUserId = participants.FirstOrDefault(id => id != userId);
                string displayName;

                if (otherUserId != null && !chat.IsGroup)
                {
                    var otherUser = await _supabase.From<User>()
                        .Filter("id", Operator.Equals, otherUserId.ToString())
                        .Single();

                    displayName = otherUser?.DisplayName ?? otherUser?.Username ?? "Unknown";
                }
                else if (chat.IsGroup)
                {
                    displayName = chat.GroupName ?? "Group Chat";
                }
                else
                {
                    displayName = "Unknown";
                }

                chats.Add(new ChatDto
                {
                    Id = chat.Id,
                    IsGroup = chat.IsGroup,
                    GroupName = chat.GroupName,
                    CreatedAt = chat.CreatedAt,
                    LastMessageAt = chat.LastMessageAt,
                    DisplayName = displayName
                });
            }

            return chats;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetUserChats: {ex}");
            throw;
        }
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
            // Преобразуем Guid в строку для фильтрации
            var response = await _supabase
                .From<DataMessage>()
                .Filter("chat_id", Operator.Equals, chatId.ToString())
                .Order("created_at", Ordering.Ascending)
                .Get();

            var messages = new List<Message>();
            var userIds = response.Models.Select(m => m.SenderId).Distinct().ToList();

            // Получаем информацию о пользователях
            var usersResponse = await _supabase
                .From<User>()
                .Filter("id", Operator.In, userIds.Select(u => u.ToString()).ToList())
                .Get();

            var userDict = usersResponse.Models.ToDictionary(u => u.Id, u => u);

            foreach (var msg in response.Models)
            {
                var user = userDict.GetValueOrDefault(msg.SenderId);
                messages.Add(new Message
                {
                    Id = msg.Id,
                    ChatId = msg.ChatId,
                    SenderId = msg.SenderId,
                    Content = msg.Content,
                    CreatedAt = msg.CreatedAt,
                    IsCurrentUser = msg.SenderId == currentUserId,
                    SenderName = msg.SenderId == currentUserId ? "You" : user?.DisplayName ?? user?.Username ?? "Unknown"
                });
            }

            return messages;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetChatMessages error: {ex}");
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
                .Filter("chat_id", Operator.Equals, chatId.ToString())
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