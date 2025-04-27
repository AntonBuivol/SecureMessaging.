using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureMessaging.Models;
using SecureMessaging.Services;
using System.Collections.ObjectModel;

namespace SecureMessaging.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly ChatService _chatService;
    private readonly SignalRService _signalRService;
    private readonly AuthService _authService;

    [ObservableProperty]
    private Chat _chat;

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

    [RelayCommand]
    private async Task LoadMessages()
    {
        var messages = await _chatService.GetChatMessages(Chat.Id, _authService.GetCurrentUserId());

        Messages.Clear();
        foreach (var message in messages)
        {
            Messages.Insert(0, message);
        }
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(MessageText))
        {
            return;
        }

        await _signalRService.SendMessage(Chat.Id, MessageText);
        MessageText = string.Empty;
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