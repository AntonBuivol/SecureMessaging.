using SecureMessaging.Models;
using Supabase;
using System.Diagnostics;
using static Supabase.Postgrest.Constants;

namespace SecureMessaging.Services;

public class ChatService
{
    private readonly Supabase.Client _supabase;

    public ChatService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<List<Chat>> GetUserChats(Guid userId)
    {
        // Get user's chat IDs first
        var userChats = await _supabase
            .From<UserChat>()
            .Select(x => new object[] { x.ChatId })
            .Where(x => x.UserId == userId)
            .Get();

        if (userChats.Models.Count == 0)
            return new List<Chat>();

        // Convert to array of Guid for the IN clause
        var chatIds = userChats.Models.Select(x => x.ChatId).ToArray();

        // Get chats using the IN operator with the array
        var response = await _supabase
            .From<Chat>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.In, chatIds)
            .Order("last_message_at", Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models;
    }

    public async Task<List<Message>> GetChatMessages(Guid chatId, Guid userId)
    {
        var response = await _supabase
            .From<Message>()
            .Filter("chat_id", Supabase.Postgrest.Constants.Operator.Equals, chatId)
            .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        var messages = response.Models;

        // Set IsCurrentUser flag
        foreach (var message in messages)
        {
            message.IsCurrentUser = message.SenderId == userId;
        }

        return messages;
    }

    public async Task<List<User>> GetChatParticipants(Guid chatId)
    {
        try
        {
            // Получаем связи пользователей с чатом
            var response = await _supabase
                .From<UserChat>()
                .Select("user_id")
                .Filter("chat_id", Operator.Equals, chatId.ToString())
                .Get();

            var userIds = response.Models
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

            if (userIds.Count == 0) return new List<User>();

            // Получаем данные пользователей
            var usersResponse = await _supabase
                .From<User>()
                .Filter("id", Operator.In, userIds.Select(id => id.ToString()).ToList())
                .Get();

            return usersResponse.Models;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting participants: {ex}");
            return new List<User>();
        }
    }

    public async Task<Chat> GetChat(Guid chatId)
    {
        try
        {
            // Преобразуем Guid в строку для фильтрации
            var response = await _supabase
                .From<Chat>()
                .Filter("id", Operator.Equals, chatId.ToString())
                .Single();

            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting chat: {ex}");
            throw;
        }
    }

    public async Task StartPrivateChat(Guid currentUserId, Guid otherUserId)
    {
        await _supabase
            .Rpc("start_private_chat", new Dictionary<string, object>
            {
                { "user1_id", currentUserId },
                { "user2_id", otherUserId }
            });
    }


}