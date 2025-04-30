using SecureMessaging.ViewModels;

namespace SecureMessaging.Views;

public partial class ChatPage : ContentPage
{
    public ChatPage(ChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (BindingContext is ChatViewModel viewModel)
        {
            if (Shell.Current.CurrentState.Location.OriginalString.Contains("ChatPage"))
            {
                var parameters = Shell.Current.CurrentState.Location.OriginalString
                    .Split('?')[1]
                    .Split('&')
                    .ToDictionary(
                        c => c.Split('=')[0],
                        c => (object)Uri.UnescapeDataString(c.Split('=')[1]));

                viewModel.ApplyQueryAttributes(parameters);
                viewModel.LoadMessagesCommand.ExecuteAsync(null);
            }
        }
    }
}