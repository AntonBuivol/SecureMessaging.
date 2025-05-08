using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SecureMessaging.Server.Models;
using SecureMessaging.Server.Services;
using Supabase.Postgrest.Exceptions;
using System.Security.Claims;

namespace SecureMessaging.Server.Hubs;

public class AuthHub : Hub
{
    private readonly AuthService _authService;
    private readonly DeviceService _deviceService;
    private readonly ILogger<AuthHub> _logger;

    public AuthHub(AuthService authService, DeviceService deviceService, ILogger<AuthHub> logger)
    {
        _authService = authService;
        _deviceService = deviceService;
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

    [Authorize]
    public async Task<List<DeviceDto>> GetUserDevices()
    {
        var userId = GetUserId();
        return await _deviceService.GetUserDevices(userId);
    }

    [Authorize]
    public async Task SetPrimaryDevice(Guid deviceId)
    {
        try
        {
            var userId = GetUserId();
            await _deviceService.SetPrimaryDevice(deviceId, userId);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError(ex, "Device authorization failed");
            throw new HubException(ex.Message);
        }
        catch (PostgrestException ex)
        {
            _logger.LogError(ex, "Database operation failed");
            throw new HubException("Failed to update device settings");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error setting primary device");
            throw new HubException("An unexpected error occurred");
        }
    }

    [Authorize]
    public async Task RemoveDevice(Guid deviceId)
    {
        var userId = GetUserId();
        await _deviceService.RemoveDevice(deviceId, userId);
    }

    [Authorize]
    public async Task<DeviceDto> GetCurrentDevice()
    {
        try
        {
            var userId = GetUserId();
            var httpContext = Context.GetHttpContext();

            if (httpContext == null)
            {
                throw new HubException("Unable to access request context");
            }

            var deviceName = httpContext.Request.Headers["Device-Name"].FirstOrDefault();

            if (string.IsNullOrEmpty(deviceName))
            {
                // Try to get the most recently active device as fallback
                var devices = await _deviceService.GetUserDevices(userId);
                var currentDevice = devices
                    .OrderByDescending(d => d.LastActive)
                    .FirstOrDefault();

                if (currentDevice == null)
                {
                    throw new HubException("No devices found for user");
                }

                return currentDevice;
            }

            return await _deviceService.GetCurrentDevice(userId, deviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current device");
            throw new HubException("Failed to identify current device. Please try again.");
        }
    }
}