using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using SecureMessaging.Models;
using Supabase;
using Supabase.Postgrest;
using Device = SecureMessaging.Models.Device;

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

    public async Task<(bool Success, string ErrorMessage)> Login(string username, string password)
    {
        try
        {
            var deviceName = DeviceInfo.Name;
            var deviceInfo = $"{DeviceInfo.Platform} {DeviceInfo.Version} {DeviceInfo.Model}";


            var user = await _supabase.From<User>()
                .Where(x => x.Username == username)
                .Single();

            if (user == null || user.PasswordHash != HashPassword(password))
            {
                return (false, "Invalid username or password");
            }

            // New approach - get all devices and filter locally
            var allDevices = await _supabase.From<Device>()
                .Where(x => x.UserId == user.Id)
                .Get();

            var existingDevice = allDevices.Models.FirstOrDefault(d =>
                d.DeviceName == deviceName &&
                d.DeviceInfo == deviceInfo);

            if (existingDevice != null)
            {
                // Update only the current device
                await _supabase.From<Device>()
                    .Where(x => x.Id == existingDevice.Id)
                    .Set(x => x.LastActive, DateTime.UtcNow)
                    .Set(x => x.IsCurrent, true)
                    .Update();
            }
            else
            {
                // First, set all devices as non-current
                await _supabase.From<Device>()
                    .Where(x => x.UserId == user.Id)
                    .Set(x => x.IsCurrent, false)
                    .Update();

                // Create new device
                var newDevice = new Device
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    DeviceName = deviceName,
                    DeviceInfo = deviceInfo,
                    IsPrimary = false,
                    IsCurrent = true,
                    CreatedAt = DateTime.UtcNow,
                    LastActive = DateTime.UtcNow
                };
                var response = await _supabase.From<Device>().Insert(newDevice);
            }

            var jwtToken = GenerateJwtToken(user.Id);
            await SecureStorage.SetAsync(AuthTokenKey, jwtToken);

            var signalRService = MauiProgram.Services.GetService<SignalRService>();
            await signalRService.Connect();

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Login failed: {ex}");
            return (false, "Login failed. Please try again.");
        }
    }

    public async Task<(bool Success, string ErrorMessage)> Register(string username, string password, string displayName)
    {
        try
        {
            var deviceName = DeviceInfo.Name;
            var deviceInfo = $"{DeviceInfo.Platform} {DeviceInfo.Version}";

            var existingUser = await _supabase.From<User>()
                .Where(x => x.Username == username)
                .Single();

            if (existingUser != null)
            {
                return (false, "Username is already taken");
            }

            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                PasswordHash = HashPassword(password),
                DisplayName = displayName ?? username,
                CreatedAt = DateTime.UtcNow,
                IsRestricted = false
            };

            var response = await _supabase.From<User>().Insert(newUser);
            var createdUser = response.Models.First();

            await AddDeviceForUser(createdUser.Id, deviceName, deviceInfo, isPrimary: true, isCurrent: true);

            var jwtToken = GenerateJwtToken(createdUser.Id);
            await SecureStorage.SetAsync(AuthTokenKey, jwtToken);

            // Добавляем ожидание подключения SignalR
            var signalRService = MauiProgram.Services.GetService<SignalRService>();
            await signalRService.Connect();

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Registration error: {ex.Message}");
            return (false, "Registration failed. Please try again.");
        }
    }

    private async Task AddDeviceForUser(Guid userId, string deviceName, string deviceInfo, bool isPrimary, bool isCurrent)
    {
        // Check if device already exists
        var existingDevice = await _supabase.From<Device>()
            .Where(x => x.UserId == userId && x.DeviceInfo == deviceInfo)
            .Single();

        if (existingDevice != null)
        {
            // Update existing device
            existingDevice.LastActive = DateTime.UtcNow;
            existingDevice.IsCurrent = isCurrent;
            await _supabase.From<Device>().Update(existingDevice);
        }
        else
        {
            // Create new device
            var newDevice = new Device
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DeviceName = deviceName,
                DeviceInfo = deviceInfo,
                IsPrimary = isPrimary,
                IsCurrent = isCurrent,
                CreatedAt = DateTime.UtcNow,
                LastActive = DateTime.UtcNow
            };

            await _supabase.From<Device>().Insert(newDevice);
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
            var token = SecureStorage.GetAsync(AuthTokenKey).GetAwaiter().GetResult();
            if (string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("Token is empty");
                return Guid.Empty;
            }

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // Проверяем все возможные варианты claim'ов
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier ||
                c.Type == JwtRegisteredClaimNames.Sub ||
                c.Type == "nameid")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                Debug.WriteLine($"Invalid user ID in token. Claims: {string.Join(", ", jwtToken.Claims.Select(c => $"{c.Type}={c.Value}"))}");
                return Guid.Empty;
            }

            Debug.WriteLine($"Current user ID: {userId}");
            return userId;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting user ID: {ex}");
            return Guid.Empty;
        }
    }
}