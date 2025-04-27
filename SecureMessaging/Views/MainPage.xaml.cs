using SecureMessaging.ViewModels;

namespace SecureMessaging.Views;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((MainViewModel)BindingContext).LoadChatsCommand.ExecuteAsync(null);
    }
}