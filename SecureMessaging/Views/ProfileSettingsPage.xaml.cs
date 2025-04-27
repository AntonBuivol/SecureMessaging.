using SecureMessaging.ViewModels;

namespace SecureMessaging.Views;

public partial class ProfileSettingsPage : ContentPage
{
    public ProfileSettingsPage(ProfileSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((ProfileSettingsViewModel)BindingContext).LoadProfileCommand.ExecuteAsync(null);
    }
}