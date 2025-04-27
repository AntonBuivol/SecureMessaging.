using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureMessaging.Models;
using SecureMessaging.Services;

namespace SecureMessaging.ViewModels;

public partial class ProfileSettingsViewModel : ObservableObject
{
    private readonly UserService _userService;
    private readonly AuthService _authService;

    [ObservableProperty]
    private string _username;

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string _about;

    [ObservableProperty]
    private string _errorMessage;

    [ObservableProperty]
    private string _successMessage;

    public ProfileSettingsViewModel(
        UserService userService,
        AuthService authService)
    {
        _userService = userService;
        _authService = authService;
    }

    [RelayCommand]
    private async Task LoadProfile()
    {
        var userId = _authService.GetCurrentUserId();
        var user = await _userService.GetUser(userId);

        Username = user.Username;
        DisplayName = user.DisplayName;
        About = user.About;
    }

    [RelayCommand]
    private async Task UpdateProfile()
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            ErrorMessage = "Display name is required";
            return;
        }

        var userId = _authService.GetCurrentUserId();
        await _userService.UpdateProfile(userId, DisplayName, About);

        SuccessMessage = "Profile updated successfully";
        await Task.Delay(3000);
        SuccessMessage = string.Empty;
    }

    [RelayCommand]
    private async Task Logout()
    {
        await _authService.Logout();
        await Shell.Current.GoToAsync("//LoginPage");
    }
}