using SecureMessaging.Models;
using Supabase;
using Supabase.Postgrest;
using System.Diagnostics;
using static Supabase.Postgrest.Constants;

namespace SecureMessaging.Services;

public class UserService
{
    private readonly Supabase.Client _supabase;

    public UserService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<User> GetUser(Guid userId)
    {
        return await _supabase.From<User>()
            .Filter("id", Operator.Equals, userId.ToString())
            .Single();
    }

    public async Task UpdateProfile(Guid userId, string displayName, string about)
    {
        var user = await _supabase
            .From<User>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, userId)
            .Single();

        if (user != null)
        {
            user.DisplayName = displayName;
            user.About = about;
            await _supabase.From<User>().Update(user);
        }
    }

    public async Task<List<User>> SearchUsers(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<User>();
        }

        var response = await _supabase
            .From<User>()
            .Where(x => x.Username.Contains(query) || x.DisplayName.Contains(query))
            .Get();

        return response.Models;
    }

    public async Task ToggleRestrictions(Guid userId, bool isRestricted)
    {
        try
        {
            // Получаем пользователя
            var response = await _supabase.From<User>()
                .Filter("id", Operator.Equals, userId.ToString())
                .Single();

            if (response != null)
            {
                // Обновляем только нужное поле
                await _supabase.From<User>()
                    .Filter("id", Operator.Equals, userId.ToString())
                    .Set(x => x.IsRestricted, isRestricted)
                    .Update();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error toggling restrictions: {ex}");
            throw;
        }
    }
}