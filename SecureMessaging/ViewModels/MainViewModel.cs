﻿using CommunityToolkit.Mvvm.ComponentModel;
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
        if (IsRefreshing) return;

        IsRefreshing = true;

        try
        {
            // Пробуем подключиться с таймаутом
            var connectTask = _signalRService.Connect();
            if (await Task.WhenAny(connectTask, Task.Delay(10000)) != connectTask)
            {
                throw new TimeoutException("Connection timeout");
            }

            var chats = await _signalRService.GetUserChats();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Chats.Clear();
                foreach (var chat in chats)
                {
                    Chats.Add(chat);
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading chats: {ex}");
            await Shell.Current.DisplayAlert("Connection Error",
                "Could not connect to server. Please check your internet connection and try again.", "OK");
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
            if (chat == null || chat.Id == Guid.Empty) return;

            NavigationData.CurrentChatId = chat.Id;
            await Shell.Current.GoToAsync("///ChatPage");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Navigation error: {ex}");
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