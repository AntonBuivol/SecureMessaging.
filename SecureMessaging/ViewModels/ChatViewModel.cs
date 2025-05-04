using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureMessaging.Models;
using SecureMessaging.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Web;

namespace SecureMessaging.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly ChatService _chatService;
    private readonly SignalRService _signalRService;
    private readonly AuthService _authService;

    [ObservableProperty]
    private Chat _chat;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _otherUserDisplayName;

    [ObservableProperty]
    private string _messageText;

    [ObservableProperty]
    private ObservableCollection<Message> _messages;

    public ChatViewModel(
        ChatService chatService,
        SignalRService signalRService,
        AuthService authService)
    {
        _chatService = chatService;
        _signalRService = signalRService;
        _authService = authService;

        Messages = new ObservableCollection<Message>();

        _signalRService.MessageReceived += OnMessageReceived;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            if (query != null && query.TryGetValue("Chat", out var chatObj) && chatObj is Chat chat)
            {
                Chat = chat;
                await LoadChatDetails();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error applying query: {ex}");
            await Shell.Current.DisplayAlert("Error", "Failed to load chat", "OK");
            await Shell.Current.GoToAsync("..");
        }
    }

    [RelayCommand]
    public async Task LoadChat(Guid chatId)
    {
        try
        {
            var currentUserId = _authService.GetCurrentUserId();
            if (currentUserId == Guid.Empty)
            {
                Debug.WriteLine("User not authenticated");
                await Shell.Current.GoToAsync("..");
                return;
            }

            var chatWithParticipants = await _chatService.GetChatWithParticipants(chatId, currentUserId);

            if (chatWithParticipants?.Chat == null)
            {
                Debug.WriteLine("Chat not found");
                await Shell.Current.GoToAsync("..");
                return;
            }

            Chat = chatWithParticipants.Chat;

            // Set title
            if (!Chat.IsGroup && chatWithParticipants.Participants?.Count > 0)
            {
                var otherUser = chatWithParticipants.Participants.First();
                Title = otherUser.DisplayName ?? otherUser.Username ?? "Unknown";
            }
            else
            {
                Title = Chat.GroupName ?? "Group Chat";
            }

            await LoadMessages();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading chat: {ex}");
            await Shell.Current.DisplayAlert("Error", "Failed to load chat", "OK");
            await Shell.Current.GoToAsync("..");
        }
    }

    private async Task LoadChatDetails()
    {
        if (Chat?.Id == null) return;

        try
        {
            var currentUserId = _authService.GetCurrentUserId();
            var chatWithParticipants = await _chatService.GetChatWithParticipants(Chat.Id, currentUserId);

            // Update chat details
            Chat = chatWithParticipants.Chat;

            // Set title based on chat type
            if (!Chat.IsGroup && chatWithParticipants.Participants.Count > 0)
            {
                var otherUser = chatWithParticipants.Participants.First();
                Title = otherUser.DisplayName ?? otherUser.Username;
            }
            else
            {
                Title = Chat.GroupName ?? "Group Chat";
            }

            await LoadMessages();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading chat details: {ex}");
            throw;
        }
    }

    [RelayCommand]
    private async Task LoadMessages()
    {
        if (Chat?.Id == null)
        {
            Debug.WriteLine("Chat ID is null");
            return;
        }

        try
        {
            Debug.WriteLine($"Loading messages for chat {Chat.Id}...");
            var currentUserId = _authService.GetCurrentUserId();

            if (currentUserId == Guid.Empty)
            {
                Debug.WriteLine("Current user ID is empty");
                return;
            }

            var messages = await _signalRService.GetChatMessages(Chat.Id);

            if (messages == null)
            {
                Debug.WriteLine("Received null messages list");
                return;
            }

            Debug.WriteLine($"Received {messages.Count} messages:");
            foreach (var msg in messages)
            {
                Debug.WriteLine($"Msg: {msg.Content} | Sender: {msg.SenderId} | At: {msg.CreatedAt} | Current: {msg.IsCurrentUser} | Name: {msg.SenderName}");
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Messages.Clear();
                foreach (var msg in messages.OrderBy(m => m.CreatedAt))
                {
                    Messages.Add(msg);
                }
                Debug.WriteLine($"Now showing {Messages.Count} messages");
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadMessages error: {ex}");
            await Shell.Current.DisplayAlert("Error", "Failed to load messages", "OK");
        }
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(MessageText)) return;

        try
        {
            var currentUserId = _authService.GetCurrentUserId();
            if (currentUserId == Guid.Empty) return;

            // Отправляем сообщение через SignalR
            await _signalRService.SendMessage(Chat.Id, MessageText);

            // Очищаем поле ввода
            MessageText = string.Empty;

            // Обновляем список сообщений
            await LoadMessages();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error sending message: {ex}");
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }


    private void OnMessageReceived(Message message)
    {
        if (message.ChatId == Chat.Id)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                // Обновляем весь список сообщений
                await LoadMessages();
            });
        }
    }
}