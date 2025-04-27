using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SecureMessaging.Models;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace SecureMessaging.Services;

public class AuthService
{
    private readonly Supabase.Client _supabase;
    private readonly string _jwtSecret;
    private const string AuthTokenKey = "auth_token";

    public AuthService(Supabase.Client supabase, string jwtSecret)
    {
        _supabase = supabase;
        _jwtSecret = jwtSecret;
    }

    public string GenerateJwtToken(Guid userId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtSecret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public async Task<bool> Register(string username, string password, string displayName)
    {
        try
        {
            var deviceName = DeviceInfo.Name;
            var deviceInfo = $"{DeviceInfo.Platform} {DeviceInfo.Version}";

            var options = new SignUpOptions
            {
                Data = new Dictionary<string, object>
                {
                    { "display_name", displayName },
                    { "device_name", deviceName },
                    { "device_info", deviceInfo }
                }
            };

            var session = await _supabase.Auth.SignUp(username, password, options);

            if (session?.User?.Id == null)
            {
                return false;
            }

            // Генерируем JWT токен
            var jwtToken = GenerateJwtToken(Guid.Parse(session.User.Id));
            await SecureStorage.SetAsync(AuthTokenKey, jwtToken);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Registration error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> Login(string username, string password)
    {
        try
        {
            var deviceName = DeviceInfo.Name;
            var deviceInfo = $"{DeviceInfo.Platform} {DeviceInfo.Version}";

            var session = await _supabase.Auth.SignIn(username, password);

            if (session?.User?.Id == null)
            {
                return false;
            }

            // Генерируем JWT токен
            var jwtToken = GenerateJwtToken(Guid.Parse(session.User.Id));
            await SecureStorage.SetAsync(AuthTokenKey, jwtToken);

            // Обновляем метаданные устройства
            var attributes = new UserAttributes
            {
                Data = new Dictionary<string, object>
                {
                    { "device_name", deviceName },
                    { "device_info", deviceInfo },
                    { "last_login", DateTime.UtcNow }
                }
            };
            await _supabase.Auth.Update(attributes);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsUserLoggedIn()
    {
        var token = await SecureStorage.GetAsync(AuthTokenKey);
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            return jwtToken.ValidTo > DateTime.UtcNow;
        }
        catch
        {
            return false;
        }
    }

    public async Task Logout()
    {
        await _supabase.Auth.SignOut();
        SecureStorage.Remove(AuthTokenKey);
    }

    public Guid GetCurrentUserId()
    {
        var token = SecureStorage.GetAsync(AuthTokenKey).Result;
        if (string.IsNullOrEmpty(token))
        {
            return Guid.Empty;
        }

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        return Guid.Parse(userId ?? string.Empty);
    }
}