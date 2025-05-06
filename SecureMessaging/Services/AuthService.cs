using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SecureMessaging.Services;

public class AuthService
{
    private readonly string _hubUrl;
    private HubConnection _hubConnection;
    private const string AuthTokenKey = "auth_token";

    public AuthService(string hubUrl)
    {
        _hubUrl = hubUrl;
    }

    private async Task EnsureHubConnected(bool requireAuth = false)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                if (requireAuth)
                {
                    options.AccessTokenProvider = async () =>
                    {
                        var token = await SecureStorage.GetAsync(AuthTokenKey);
                        return token;
                    };
                }
            })
            .WithAutomaticReconnect()
            .Build();

        try
        {
            await _hubConnection.StartAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SignalR Connection Error: {ex}");
            throw;
        }
    }

    public async Task<(bool Success, string ErrorMessage)> Register(string username, string password, string displayName)
    {
        try
        {
            await EnsureHubConnected(requireAuth: false);

            var deviceName = DeviceInfo.Name;
            var deviceInfo = $"{DeviceInfo.Platform} {DeviceInfo.Version}";

            var token = await _hubConnection.InvokeAsync<string>(
                "Register",
                username,
                password,
                displayName ?? username,
                deviceName,
                deviceInfo);

            await SecureStorage.SetAsync(AuthTokenKey, token);

            // Now disconnect and reconnect with the new token
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
            _hubConnection = null;

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Registration error: {ex.Message}");
            return (false, ex is HubException hubEx ? hubEx.Message : "Registration failed. Please try again.");
        }
    }

    public async Task<(bool Success, string ErrorMessage)> Login(string username, string password)
    {
        try
        {
            await EnsureHubConnected(requireAuth: false);

            // Get device info using MAUI APIs (client-side only)
            var deviceName = DeviceInfo.Name ?? "Unknown Device";
            var deviceInfo = $"{DeviceInfo.Platform} {DeviceInfo.Version} {DeviceInfo.Model}" ?? "Unknown Info";

            var token = await _hubConnection.InvokeAsync<string>(
                "Login",
                username,
                password,
                deviceName,
                deviceInfo);

            if (string.IsNullOrEmpty(token))
            {
                return (false, "Login failed - no token received");
            }

            await SecureStorage.SetAsync(AuthTokenKey, token);
            return (true, string.Empty);
        }
        catch (HubException hubEx)
        {
            return (false, hubEx.Message);
        }
        catch (Exception ex)
        {
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
        try
        {
            await EnsureHubConnected(requireAuth: true);
            await _hubConnection.InvokeAsync("Logout");
            SecureStorage.Remove(AuthTokenKey);
            await _hubConnection.StopAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Logout error: {ex}");
        }
        finally
        {
            SecureStorage.Remove(AuthTokenKey);
        }
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