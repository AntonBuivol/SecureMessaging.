using SecureMessaging.ViewModels;

namespace SecureMessaging.Views;

public partial class AppSettingsPage : ContentPage
{
    public AppSettingsPage(AppSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((AppSettingsViewModel)BindingContext).LoadSettingsCommand.ExecuteAsync(null);
    }
}