using SecureMessaging.ViewModels;

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
        await ((ChatViewModel)BindingContext).LoadMessagesCommand.ExecuteAsync(null);
    }
}