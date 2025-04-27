using SecureMessaging.ViewModels;

namespace SecureMessaging.Views;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnPasswordFocused(object sender, FocusEventArgs e)
    {
        if (BindingContext is RegisterViewModel vm)
        {
            vm.ShowPasswordRequirements = true;
        }
    }
}