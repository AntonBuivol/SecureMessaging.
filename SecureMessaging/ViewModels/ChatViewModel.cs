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
    [NotifyPropertyChangedFor(nameof(Title))]
    private Chat _chat;

    public string Title => Chat?.DisplayName ?? "Chat";

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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            if (query != null && query.TryGetValue("Chat", out var chatObj))
            {
                if (chatObj is Chat chat)
                {
                    Chat = chat;
                    OnPropertyChanged(nameof(Chat));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error applying query: {ex}");
        }
    }

    [RelayCommand]
    private async Task LoadMessages()
    {
        if (Chat?.Id == null) return;

        try
        {
            var messages = await _chatService.GetChatMessages(Chat.Id, _authService.GetCurrentUserId());
            Messages.Clear();
            foreach (var msg in messages.OrderBy(m => m.CreatedAt))
            {
                Messages.Add(msg);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading messages: {ex}");
        }
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(MessageText)) return;

        try
        {
            var currentUserId = _authService.GetCurrentUserId();
            Debug.WriteLine($"Sending message - ChatId: {Chat?.Id}, UserId: {currentUserId}");

            if (Chat?.Id == null || currentUserId == Guid.Empty)
            {
                Debug.WriteLine("Validation failed - Chat or User not loaded");
                await Shell.Current.DisplayAlert("Error",
                    Chat?.Id == null ? "Chat not loaded" : "User not authenticated",
                    "OK");
                return;
            }

            var message = new Message
            {
                ChatId = Chat.Id,
                SenderId = currentUserId,
                Content = MessageText,
                CreatedAt = DateTime.UtcNow,
                IsCurrentUser = true,
                SenderName = "You"
            };

            await _signalRService.SendMessage(Chat.Id, MessageText);
            Messages.Insert(0, message);
            MessageText = string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error sending message: {ex}");
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private void OnMessageReceived(Message message)
    {
        if (message.ChatId == Chat.Id) // This will now work
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Messages.Insert(0, message);
            });
        }
    }
}