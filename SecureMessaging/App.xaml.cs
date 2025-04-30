using SecureMessaging.Services;
using SecureMessaging.ViewModels;
using SecureMessaging.Views;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;

namespace SecureMessaging;

public partial class App : Application
{
    private readonly AuthService _authService;
    private readonly SignalRService _signalRService;

    public App(AuthService authService, SignalRService signalRService)
    {
        InitializeComponent();

        _authService = authService;
        _signalRService = signalRService;

        MainPage = new AppShell();
    }

    protected override async void OnStart()
    {
        base.OnStart();

        // Проверяем токен
        var token = await SecureStorage.GetAsync("auth_token");
        Debug.WriteLine($"Token exists: {!string.IsNullOrEmpty(token)}");

        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                Debug.WriteLine($"Token contents: {string.Join(", ", jwtToken.Claims.Select(c => $"{c.Type}={c.Value}"))}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Token validation error: {ex}");
            }
        }

        var isLoggedIn = await _authService.IsUserLoggedIn();
        Debug.WriteLine($"User is logged in: {isLoggedIn}");

        if (isLoggedIn)
        {
            try
            {
                await _signalRService.Connect();
                await Shell.Current.GoToAsync("//MainPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup error: {ex}");
                await Shell.Current.GoToAsync("//LoginPage");
            }
        }
        else
        {
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}