using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using SecureMessaging.Converters;
using SecureMessaging.Services;
using SecureMessaging.ViewModels;
using SecureMessaging.Views;
using Supabase;

namespace SecureMessaging;

public static class MauiProgram
{
    public static IServiceProvider Services { get; private set; }

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .UseMauiCommunityToolkit();

        // Configuration
        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        // Supabase configuration
        var supabaseUrl = "https://hgmogmeywxfdrggfdfzl.supabase.co";
        var supabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImhnbW9nbWV5d3hmZHJnZ2ZkZnpsIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NDMyNjMyNDMsImV4cCI6MjA1ODgzOTI0M30.75mo-uchFP1Mf9RzC-2Jn-De73Rn-agxpcofhSp2DWo";
        var jwtKey = "D3kGdmfJBfNJepC0J37w7Wa6HtmEAVFJVXIFEhXXr0BRlCFB8hREAFi4lhjapOU44+rdyv6ZtcneOiU4sQzVew==";
        var signalRHubUrl = "https://7342-145-14-21-133.ngrok-free.app/chatHub";

        var supabaseOptions = new SupabaseOptions
        {
            AutoConnectRealtime = true
        };

        // Register services in correct order with all required dependencies
        var supabaseClient = new Client(supabaseUrl, supabaseKey, supabaseOptions);
        builder.Services.AddSingleton(supabaseClient);

        builder.Services.AddSingleton<AuthService>(provider =>
    new AuthService("https://7342-145-14-21-133.ngrok-free.app/authHub"));

        builder.Services.AddSingleton<SignalRService>(provider =>
            new SignalRService(
                provider.GetRequiredService<AuthService>(),
                signalRHubUrl));

        // Other services
        builder.Services.AddSingleton<ChatService>();
        builder.Services.AddSingleton<DeviceService>();
        builder.Services.AddSingleton<UserService>();

        // Register view models and views...
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<ChatViewModel>();
        builder.Services.AddTransient<ProfileSettingsViewModel>();
        builder.Services.AddTransient<AppSettingsViewModel>();

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ChatPage>();
        builder.Services.AddTransient<AppSettingsViewModel>(provider =>
    new AppSettingsViewModel(
        provider.GetRequiredService<DeviceService>(),
        provider.GetRequiredService<UserService>(),
        provider.GetRequiredService<AuthService>()));

        builder.Services.AddTransient<ProfileSettingsViewModel>(provider =>
            new ProfileSettingsViewModel(
                provider.GetRequiredService<UserService>(),
                provider.GetRequiredService<AuthService>()));

        builder.Services.AddSingleton<InverseBoolConverter>();
        builder.Services.AddSingleton<DateTimeToTimeConverter>();

        builder.UseMauiApp<App>().UseMauiCommunityToolkit();

        var app = builder.Build();
        Services = app.Services; // Сохраняем ServiceProvider
        return app;
    }
}