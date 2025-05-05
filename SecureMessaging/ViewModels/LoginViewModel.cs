using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureMessaging.Services;

namespace SecureMessaging.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _authService;

    [ObservableProperty]
    private string _username;

    [ObservableProperty]
    private string _password;

    [ObservableProperty]
    private string _errorMessage;

    public LoginViewModel(AuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task Login()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Username and password are required";
            return;
        }

        try
        {
            var (success, error) = await _authService.Login(Username, Password);

            if (success)
            {
                // Даем время на установку соединения
                await Task.Delay(500);
                await Shell.Current.GoToAsync("//MainPage");
            }
            else
            {
                ErrorMessage = error ?? "Invalid username or password";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Login failed. Please try again.";
            Console.WriteLine($"Login error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task NavigateToRegister()
    {
        await Shell.Current.GoToAsync("//RegisterPage");
    }
}