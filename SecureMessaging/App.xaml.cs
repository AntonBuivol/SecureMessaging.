using SecureMessaging.Services;
using SecureMessaging.ViewModels;
using SecureMessaging.Views;

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

        // Check if user is already logged in
        var isLoggedIn = await _authService.IsUserLoggedIn();

        if (isLoggedIn)
        {
            // Connect to SignalR
            await _signalRService.Connect();

            // Navigate to main page
            await Shell.Current.GoToAsync("//MainPage");
        }
        else
        {
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}