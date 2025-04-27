using CommunityToolkit.Mvvm.ComponentModel;
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

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match";
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            DisplayName = Username;
        }

        var success = await _authService.Register(Username, Password, DisplayName);

        if (success)
        {
            await Shell.Current.GoToAsync("//MainPage");
        }
        else
        {
            ErrorMessage = "Registration failed. Username may already be taken.";
        }
    }

    [RelayCommand]
    private async Task NavigateToLogin()
    {
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
