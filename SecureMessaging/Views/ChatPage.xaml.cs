using SecureMessaging.Models;
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
                if (NavigationData.CurrentChatId != Guid.Empty)
                {
                    await vm.LoadChat(NavigationData.CurrentChatId);
                    NavigationData.CurrentChatId = Guid.Empty; // Очищаем после использования
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