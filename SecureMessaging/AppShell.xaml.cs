using SecureMessaging.Views;

namespace SecureMessaging
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("ChatPage", typeof(ChatPage));
        }
    }
}
