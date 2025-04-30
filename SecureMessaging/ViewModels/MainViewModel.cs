using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureMessaging.Models;
using SecureMessaging.Services;
using SecureMessaging.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace SecureMessaging.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ChatService _chatService;
    private readonly UserService _userService;
    private readonly SignalRService _signalRService;
    private readonly AuthService _authService;

    [ObservableProperty]
    private ObservableCollection<Chat> _chats;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _searchQuery;

    [ObservableProperty]
    private bool _hasSearchResults;

    [ObservableProperty]
    private ObservableCollection<User> _searchResults;

    public MainViewModel(
        ChatService chatService,
        UserService userService,
        SignalRService signalRService,
        AuthService authService)
    {
        _chatService = chatService;
        _userService = userService;
        _signalRService = signalRService;
        _authService = authService;

        Chats = new ObservableCollection<Chat>();
        SearchResults = new ObservableCollection<User>();

        _signalRService.ChatStarted += OnChatStarted;
    }

    [RelayCommand]
    private async Task LoadChats()
    {
        IsRefreshing = true;

        try
        {
            var userId = _authService.GetCurrentUserId();
            Debug.WriteLine($"Loading chats for user: {userId}");

            if (userId == Guid.Empty)
            {
                await Shell.Current.DisplayAlert("Error", "Please login again", "OK");
                await Shell.Current.GoToAsync("//LoginPage");
                return;
            }

            var chats = await _chatService.GetUserChats(userId);

            Chats.Clear();
            foreach (var chat in chats)
            {
                Chats.Add(chat);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading chats: {ex}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task SearchUsers()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchResults.Clear();
            HasSearchResults = false;
            return;
        }

        var results = await _userService.SearchUsers(SearchQuery);

        SearchResults.Clear();
        foreach (var user in results)
        {
            SearchResults.Add(user);
        }

        HasSearchResults = SearchResults.Count > 0;
    }

    [RelayCommand]
    private async Task StartChat(User user)
    {
        var currentUserId = _authService.GetCurrentUserId();
        await _signalRService.StartPrivateChat(currentUserId, user.Id);
        SearchQuery = string.Empty;
        SearchResults.Clear();
    }

    [RelayCommand]
    private async Task NavigateToChat(Chat chat)
    {
        try
        {
            if (chat == null) return;

            // Создаем базовый объект чата для передачи
            var chatToPass = new Chat
            {
                Id = chat.Id,
                IsGroup = chat.IsGroup,
                DisplayName = chat.DisplayName
            };

            Debug.WriteLine($"Passing chat: {chatToPass?.Id}, Name: {chatToPass?.DisplayName}");
            Debug.WriteLine($"Navigation state: {Shell.Current.CurrentState}");

            // Для приватных чатов загружаем имя собеседника
            if (!chat.IsGroup)
            {
                var participants = await _chatService.GetChatParticipants(chat.Id);
                if (participants.Count == 2)
                {
                    var currentUserId = _authService.GetCurrentUserId();
                    var otherUser = participants.FirstOrDefault(p => p.Id != currentUserId);
                    chatToPass.DisplayName = otherUser?.DisplayName ?? otherUser?.Username ?? "Unknown";
                }
            }

            // Навигация с минимально необходимыми данными
            await Shell.Current.GoToAsync("///ChatPage", new Dictionary<string, object>
        {
            { "Chat", chatToPass }
        });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Navigation error: {ex}");
            await Shell.Current.DisplayAlert("Error", "Couldn't open chat", "OK");
        }
    }

    private void OnChatStarted(Chat chat)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Chats.Insert(0, chat);
        });
    }
}