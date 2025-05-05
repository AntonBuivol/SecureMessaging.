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

        var token = await SecureStorage.GetAsync("auth_token");

        if (!string.IsNullOrEmpty(token))
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