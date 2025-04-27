using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SecureMessaging.Models;
using Supabase;
using Supabase.Postgrest;

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

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    public string GenerateJwtToken(Guid userId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtSecret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")) // Используем "D" формат для Guid
        }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public async Task<(bool Success, string ErrorMessage)> Register(string username, string password, string displayName)
    {
        try
        {
            // Проверка существующего пользователя
            var existingUser = await _supabase.From<User>()
                .Where(x => x.Username == username)
                .Single();

            if (existingUser != null)
            {
                return (false, "Username is already taken");
            }

            // Создание нового пользователя с явным Guid
            var newUser = new User
            {
                Id = Guid.NewGuid(), // Явное создание Guid
                Username = username,
                PasswordHash = HashPassword(password),
                DisplayName = displayName,
                CreatedAt = DateTime.UtcNow,
                IsRestricted = false
            };

            var response = await _supabase.From<User>().Insert(newUser);
            var createdUser = response.Models.First();

            // Генерация JWT токена
            var jwtToken = GenerateJwtToken(createdUser.Id);
            await SecureStorage.SetAsync(AuthTokenKey, jwtToken);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Registration error: {ex.Message}");
            return (false, "Registration failed. Please try again.");
        }
    }

    public async Task<(bool Success, string ErrorMessage)> Login(string username, string password)
    {
        try
        {
            var user = await _supabase.From<User>()
                .Where(x => x.Username == username)
                .Single();

            if (user == null || user.PasswordHash != HashPassword(password))
            {
                return (false, "Invalid username or password");
            }

            // Генерация JWT токена
            var jwtToken = GenerateJwtToken(user.Id);
            await SecureStorage.SetAsync(AuthTokenKey, jwtToken);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login error: {ex.Message}");
            return (false, "Login failed. Please try again.");
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
        SecureStorage.Remove(AuthTokenKey);
    }

    public Guid GetCurrentUserId()
    {
        try
        {
            var token = SecureStorage.GetAsync(AuthTokenKey).Result;
            if (string.IsNullOrEmpty(token))
            {
                return Guid.Empty;
            }

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Guid.Empty;
            }

            return userId;
        }
        catch
        {
            return Guid.Empty;
        }
    }
}