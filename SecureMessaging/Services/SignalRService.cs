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

    public async Task Connect(bool forceReconnect = false)
    {
        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected && !forceReconnect)
            return;

        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = async () =>
                {
                    var token = await SecureStorage.GetAsync("auth_token");
                    return token;
                };
            })
            .WithAutomaticReconnect(new RetryPolicy())
            .Build();

        _hubConnection.On<Message>("ReceiveMessage", message => MessageReceived?.Invoke(message));
        _hubConnection.On<Chat>("ChatStarted", chat => ChatStarted?.Invoke(chat));

        try
        {
            await _hubConnection.StartAsync();
            Debug.WriteLine("SignalR connected successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SignalR Connection Error: {ex}");
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
        try
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                Debug.WriteLine($"Sending message to chat: {chatId}, content: {content}");
                await _hubConnection.InvokeAsync("SendMessage", chatId, content);
            }
            else
            {
                Debug.WriteLine("SignalR connection not established");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SignalR send error: {ex}");
            throw;
        }
    }

    public async Task StartPrivateChat(Guid currentUserId, Guid otherUserId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _hubConnection.InvokeAsync("StartPrivateChat", otherUserId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting private chat: {ex}");
                throw;
            }
        }
        else
        {
            throw new Exception("SignalR connection is not established");
        }
    }

    public async Task<List<Message>> GetChatMessages(Guid chatId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                return await _hubConnection.InvokeAsync<List<Message>>("GetChatMessages", chatId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting messages: {ex}");
                return new List<Message>();
            }
        }
        throw new Exception("SignalR connection not established");
    }
}