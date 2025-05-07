using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SecureMessaging.Server.Services;
using System.Security.Claims;

namespace SecureMessaging.Server.Hubs;

public class AuthHub : Hub
{
    private readonly AuthService _authService;
    private readonly ILogger<AuthHub> _logger;

    public AuthHub(AuthService authService, ILogger<AuthHub> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [AllowAnonymous]
    public async Task<string> Register(string username, string password, string displayName, string deviceName, string deviceInfo)
    {
        try
        {
            return await _authService.Register(username, password, displayName, deviceName, deviceInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed");
            throw new HubException("Registration failed");
        }
    }

    [AllowAnonymous]
    public async Task<string> Login(string username, string password, string deviceName, string deviceInfo)
    {
        try
        {
            return await _authService.Login(username, password, deviceName, deviceInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            throw new HubException("Invalid username or password");
        }
    }

    [Authorize]
    public async Task Logout()
    {
        var userId = GetUserId();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId.ToString());
    }

    private Guid GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? Context.User?.FindFirst("id")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new HubException("Invalid user ID");
        }

        return userId;
    }

    public async Task<bool> IsRestrictedUser(Guid userId)
    {
        try
        {
            return await _authService.IsRestrictedUser(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking user restrictions");
            return true;
        }
    }

    public async Task<bool> IsPrimaryDevice(Guid userId, string deviceName)
    {
        try
        {
            return await _authService.IsPrimaryDevice(userId, deviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking primary device");
            return false;
        }
    }
}