using Microsoft.AspNetCore.SignalR.Client;
using SecureMessaging.Models;
using System.Diagnostics;

namespace SecureMessaging.Services;

public class SignalRService
{
    private HubConnection _hubConnection;
    private readonly AuthService _authService;
    private readonly string _hubUrl;
    private bool _isReconnecting;

    public event Action<Message> MessageReceived;
    public event Action<Chat> ChatStarted;

    public SignalRService(AuthService authService, string hubUrl)
    {
        _authService = authService;
        _hubUrl = hubUrl;
    }

    public async Task Connect()
    {
        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = async () =>
                {
                    var token = await SecureStorage.GetAsync("auth_token");
                    return token;
                };
                // ... rest of your configuration
            })
            .Build();

        // Setup your message handlers
        _hubConnection.On<Message>("ReceiveMessage", message => MessageReceived?.Invoke(message));
        _hubConnection.On<Chat>("ChatStarted", chat => ChatStarted?.Invoke(chat));

        try
        {
            await _hubConnection.StartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SignalR Connection Error: {ex}");
            throw;
        }
    }

    private class RetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            return TimeSpan.FromSeconds(Math.Min(retryContext.PreviousRetryCount * 2, 60));
        }
    }

    public async Task Disconnect()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }

    public async Task SendMessage(Guid chatId, string content)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("SendMessage", chatId, content);
        }
    }

    public async Task StartPrivateChat(Guid currentUserId, Guid otherUserId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("StartPrivateChat", currentUserId, otherUserId);
        }
    }
}