using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SecureMessaging.Server.Models;
using Supabase;

namespace SecureMessaging.Server.Services;

public class AuthService
{
    private readonly Client _supabase;
    private readonly IConfiguration _configuration;
    private readonly DeviceService _deviceService;

    public AuthService(Client supabase, IConfiguration configuration, DeviceService deviceService)
    {
        _supabase = supabase;
        _configuration = configuration;
        _deviceService = deviceService;
    }

    public async Task<string> Register(string username, string password, string displayName, string deviceName, string deviceInfo)
    {
        // Check if username is already taken
        var existingUser = await _supabase.From<User>()
            .Where(x => x.Username == username)
            .Single();

        if (existingUser != null)
        {
            throw new Exception("Username is already taken");
        }

        // Create new user
        var newUser = new User
        {
            Username = username,
            PasswordHash = HashPassword(password),
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow
        };

        var response = await _supabase.From<User>().Insert(newUser);
        var createdUser = response.Models.First();

        // Create primary device
        await _deviceService.CreateDevice(createdUser.Id, deviceName, deviceInfo, true, true);

        return GenerateJwtToken(createdUser.Id);
    }

    public async Task<string> Login(string username, string password, string deviceName, string deviceInfo)
    {
        var user = await _supabase.From<User>()
            .Where(x => x.Username == username)
            .Single();

        if (user == null || !VerifyPassword(password, user.PasswordHash))
        {
            throw new Exception("Invalid username or password");
        }

        // Check if device already exists
        var existingDevice = await _supabase.From<Device>()
            .Where(x => x.UserId == user.Id && x.DeviceInfo == deviceInfo)
            .Single();

        if (existingDevice != null)
        {
            // Update last active and access token
            existingDevice.LastActive = DateTime.UtcNow;
            existingDevice.IsCurrent = true;
            await _supabase.From<Device>().Update(existingDevice);
        }
        else
        {
            // Create new device
            await _deviceService.CreateDevice(user.Id, deviceName, deviceInfo, false, true);
        }

        return GenerateJwtToken(user.Id);
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private bool VerifyPassword(string password, string storedHash)
    {
        return HashPassword(password) == storedHash;
    }

    private string GenerateJwtToken(Guid userId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
            new Claim("nameid", userId.ToString()), // Основной claim
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public Guid GetCurrentUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? user.FindFirst("nameid")?.Value
                       ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        throw new UnauthorizedAccessException("Invalid user ID in token");
    }
}