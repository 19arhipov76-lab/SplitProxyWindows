using System.Windows;
using SplitProxy.Core.Api;
using SplitProxy.Core.Models;

namespace SplitProxy.App;

public partial class LoginWindow : Window
{
    private readonly ServerGateway _gateway;
    public AccountSession? Session { get; private set; }

    public LoginWindow(ServerGateway gateway)
    {
        InitializeComponent();
        _gateway = gateway;
    }

    private async void Login_Click(object sender, RoutedEventArgs e) => await AuthenticateAsync(false);
    private async void Register_Click(object sender, RoutedEventArgs e) => await AuthenticateAsync(true);

    private async Task AuthenticateAsync(bool register)
    {
        SetBusy(true);
        ErrorText.Text = "";
        try
        {
            if (string.IsNullOrWhiteSpace(EmailBox.Text) || string.IsNullOrEmpty(PasswordBox.Password))
                throw new InvalidOperationException("Введите e-mail и пароль.");
            if (register && string.IsNullOrWhiteSpace(NameBox.Text))
                throw new InvalidOperationException("Для регистрации укажите имя.");

            Session = register
                ? await _gateway.RegisterAsync(NameBox.Text, EmailBox.Text, PasswordBox.Password)
                : await _gateway.LoginAsync(EmailBox.Text, PasswordBox.Password);
            DialogResult = true;
        }
        catch (Exception ex) { ErrorText.Text = ex.Message; }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy)
    {
        LoginButton.IsEnabled = RegisterButton.IsEnabled = !busy;
    }
}
