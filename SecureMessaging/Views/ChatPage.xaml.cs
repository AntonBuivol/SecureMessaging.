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
            Debug.WriteLine($"ChatPage appearing - ChatId: {vm.Chat?.Id}");

            if (vm.Chat == null)
            {
                Debug.WriteLine("Chat is null - going back");
                await Shell.Current.GoToAsync("..");
                return;
            }

            try
            {
                await vm.LoadMessagesCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading messages: {ex}");
            }
        }
    }
}