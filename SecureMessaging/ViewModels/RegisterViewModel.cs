﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureMessaging.Services;

namespace SecureMessaging.ViewModels;

public partial class RegisterViewModel : ObservableObject
{
    private readonly AuthService _authService;

    [ObservableProperty]
    private string _username;

    [ObservableProperty]
    private string _password;

    [ObservableProperty]
    private string _confirmPassword;

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string _errorMessage;

    [ObservableProperty]
    private bool _showPasswordRequirements;

    [ObservableProperty]
    private string _passwordRequirements = "• At least 6 characters";

    public RegisterViewModel(AuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task Register()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Username and password are required";
            return;
        }

        if (Password.Length < 6)
        {
            ErrorMessage = "Password must be at least 6 characters";
            return;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match";
            return;
        }

        var (success, error) = await _authService.Register(Username, Password, DisplayName ?? Username);

        if (success)
        {
            // Даем время на установку соединения
            await Task.Delay(500);
            await Shell.Current.GoToAsync("//MainPage");
        }
        else
        {
            ErrorMessage = error;
        }
    }

    [RelayCommand]
    private async Task NavigateToLogin()
    {
        await Shell.Current.GoToAsync("//LoginPage");
    }
}