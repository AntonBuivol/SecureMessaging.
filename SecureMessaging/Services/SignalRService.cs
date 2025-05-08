using Microsoft.AspNetCore.SignalR.Client;
using SecureMessaging.Models;
using System.Diagnostics;

namespace SecureMessaging.Services;

public class SignalRService
{
    private HubConnection _hubConnection;
    private readonly AuthService _authService;
    private readonly string _hubUrl;
    private bool _isConnecting;
    private CancellationTokenSource _connectionCts;

    public event Action<Message> MessageReceived;
    public event Action<Chat> ChatStarted;

    public SignalRService(AuthService authService, string hubUrl)
    {
        _authService = authService;
        _hubUrl = hubUrl;
    }

    public async Task Connect(bool forceReconnect = false)
    {
        if (_isConnecting) return;
        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected && !forceReconnect)
            return;

        _isConnecting = true;
        _connectionCts?.Cancel();
        _connectionCts = new CancellationTokenSource();

        try
        {
            // Отменяем предыдущее подключение, если было
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
            }

            var deviceName = DeviceInfo.Name;
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_hubUrl, options =>
                {
                    options.AccessTokenProvider = async () =>
                    {
                        var token = await SecureStorage.GetAsync("auth_token");
                        return token;
                    };
                    options.Headers["Device-Name"] = deviceName;
                    options.HttpMessageHandlerFactory = handler =>
                        new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
                })
                .WithAutomaticReconnect(new RetryPolicy())
                .Build();

            _hubConnection.Closed += async (error) =>
            {
                Debug.WriteLine($"Connection closed: {error?.Message}");
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await Connect(true);
            };

            _hubConnection.Reconnecting += error =>
            {
                Debug.WriteLine($"Connection reconnecting: {error?.Message}");
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += connectionId =>
            {
                Debug.WriteLine($"Connection reconnected: {connectionId}");
                return Task.CompletedTask;
            };

            // Настройка таймаутов
            _hubConnection.ServerTimeout = TimeSpan.FromSeconds(30);
            _hubConnection.HandshakeTimeout = TimeSpan.FromSeconds(15);

            // Добавляем обработчики сообщений
            _hubConnection.On<string>("AccessDenied", OnAccessDenied);
            _hubConnection.On<Message>("ReceiveMessage", OnMessageReceived);
            _hubConnection.On<Chat>("ChatStarted", OnChatStarted);

            // Пробуем подключиться несколько раз
            int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    await _hubConnection.StartAsync(_connectionCts.Token);
                    Debug.WriteLine("SignalR connected successfully");
                    break;
                }
                catch (Exception ex) when (i < maxRetries - 1)
                {
                    Debug.WriteLine($"Connection attempt {i + 1} failed: {ex.Message}");
                    await Task.Delay(2000 * (i + 1));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SignalR Connection Error: {ex}");
            throw;
        }
        finally
        {
            _isConnecting = false;
        }
    }
    private void OnAccessDenied(string message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Shell.Current.DisplayAlert("Access Denied", message, "OK");
            await Shell.Current.GoToAsync("//LoginPage");
        });
    }

    private void OnMessageReceived(Message message)
    {
        MessageReceived?.Invoke(message);
    }

    private void OnChatStarted(Chat chat)
    {
        ChatStarted?.Invoke(chat);
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

    public async Task<List<Chat>> GetUserChats()
    {
        try
        {
            if (_hubConnection?.State != HubConnectionState.Connected)
            {
                await Connect();
            }

            return await _hubConnection.InvokeAsync<List<Chat>>("GetUserChats");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting user chats: {ex}");
            return new List<Chat>();
        }
    }
}