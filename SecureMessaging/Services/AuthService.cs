using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SecureMessaging.Services;

public class AuthService
{
    private readonly string _hubUrl;
    public HubConnection _hubConnection;
    public HubConnection HubConnection => _hubConnection;

    private const string AuthTokenKey = "auth_token";

    public AuthService(string hubUrl)
    {
        _hubUrl = hubUrl;
    }

    public async Task EnsureHubConnected(bool requireAuth = false)
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

            await EnsureHubConnected(requireAuth: true);

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

            var deviceName = DeviceInfo.Name ?? "Unknown Device";
            var deviceInfo = GenerateDeviceIdentifier();

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

            await EnsureHubConnected(requireAuth: true);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private string GenerateDeviceIdentifier()
    {
        try
        {
            // Create a simple but unique device identifier
            return $"{DeviceInfo.Platform} {DeviceInfo.Version}";
        }
        catch
        {
            // Fallback to random GUID if device info isn't available
            return Guid.NewGuid().ToString();
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

    public async Task<bool> IsRestrictedUser(Guid userId)
    {
        try
        {
            await EnsureHubConnected(requireAuth: true);
            return await _hubConnection.InvokeAsync<bool>("IsRestrictedUser", userId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IsRestrictedUser error: {ex}");
            return true;
        }
    }

    public async Task<bool> IsPrimaryDevice(Guid userId, string deviceName)
    {
        try
        {
            await EnsureHubConnected(requireAuth: true);
            return await _hubConnection.InvokeAsync<bool>("IsPrimaryDevice", userId, deviceName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IsPrimaryDevice error: {ex}");
            return false;
        }
    }
}