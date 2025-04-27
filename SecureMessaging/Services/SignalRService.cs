using Microsoft.AspNetCore.SignalR.Client;
using SecureMessaging.Models;

namespace SecureMessaging.Services;

public class SignalRService
{
    private HubConnection _hubConnection;
    private readonly AuthService _authService;
    private readonly string _hubUrl;

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
        {
            return;
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = async () => await SecureStorage.GetAsync("auth_token");
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<Message>("ReceiveMessage", (message) => MessageReceived?.Invoke(message));
        _hubConnection.On<Chat>("ChatStarted", (chat) => ChatStarted?.Invoke(chat));

        await _hubConnection.StartAsync();
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