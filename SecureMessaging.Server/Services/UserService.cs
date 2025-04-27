using SecureMessaging.Server.Models;
using Supabase;

namespace SecureMessaging.Server.Services;

public class UserService
{
    private readonly Supabase.Client _supabase;

    public UserService(Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<User> GetUserById(Guid id)
    {
        return await _supabase.From<User>()
            .Where(x => x.Id == id)
            .Single();
    }

    public async Task UpdateUserProfile(Guid userId, string displayName, string about)
    {
        var user = await _supabase.From<User>()
            .Where(x => x.Id == userId)
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
        var response = await _supabase.From<User>()
            .Where(x => x.Username.Contains(query) || x.DisplayName.Contains(query))
            .Get();

        return response.Models;
    }

    public async Task ToggleRestrictions(Guid userId, bool isRestricted)
    {
        var user = await _supabase.From<User>()
            .Where(x => x.Id == userId)
            .Single();

        if (user != null)
        {
            user.IsRestricted = isRestricted;
            await _supabase.From<User>().Update(user);
        }
    }
}