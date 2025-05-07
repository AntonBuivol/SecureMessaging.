using SecureMessaging.Models;
using SecureMessaging.Services;
using SecureMessaging.ViewModels;
using System.Diagnostics;

namespace SecureMessaging.Views;

public partial class ChatPage : ContentPage
{
    public ChatPage(ChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is ChatViewModel vm)
        {
            try
            {
                var authService = MauiProgram.Services.GetService<AuthService>();
                var deviceName = DeviceInfo.Name;
                var userId = authService.GetCurrentUserId();

                if (await authService.IsRestrictedUser(userId) &&
                    !await authService.IsPrimaryDevice(userId, deviceName))
                {
                    await Shell.Current.DisplayAlert("Access Denied",
                        "Please use your primary device to access chats", "OK");
                    await Shell.Current.GoToAsync("//MainPage");
                    return;
                }

                if (NavigationData.CurrentChatId != Guid.Empty)
                {
                    await vm.LoadChat(NavigationData.CurrentChatId);
                    NavigationData.CurrentChatId = Guid.Empty;
                }
                else
                {
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex}");
                await Shell.Current.GoToAsync("..");
            }
        }
    }
}