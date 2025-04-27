using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureMessaging.Models;
using SecureMessaging.Services;
using System.Collections.ObjectModel;

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

        var userId = _authService.GetCurrentUserId();
        var chats = await _chatService.GetUserChats(userId);

        Chats.Clear();
        foreach (var chat in chats)
        {
            Chats.Add(chat);
        }

        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task SearchUsers()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchResults.Clear();
            return;
        }

        var results = await _userService.SearchUsers(SearchQuery);

        SearchResults.Clear();
        foreach (var user in results)
        {
            SearchResults.Add(user);
        }
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
        var parameters = new Dictionary<string, object>
        {
            { "Chat", chat }
        };

        await Shell.Current.GoToAsync("ChatPage", parameters);
    }

    private void OnChatStarted(Chat chat)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Chats.Insert(0, chat);
        });
    }
}