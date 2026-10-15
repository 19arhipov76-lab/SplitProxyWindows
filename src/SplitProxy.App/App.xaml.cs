using System.Windows;

namespace SplitProxy.App;

public partial class App : Application
{
    private SingleInstanceCoordinator? _single;
    private TrayIconManager? _tray;
    private MainWindow? _window;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _single = new SingleInstanceCoordinator("SplitProxy.Windows.SingleInstance");
        if (!_single.IsPrimary)
        {
            await _single.SignalPrimaryAsync();
            Shutdown();
            return;
        }

        _window = new MainWindow();
        _tray = new TrayIconManager(_window, ExitApplicationAsync);
        _single.ActivationRequested += (_, _) => Dispatcher.Invoke(() => ShowMainWindow());
        _single.StartServer();
        _window.RequestHideToTray += (_, _) => _window.Hide();
        _window.Show();
        _window.Activate();
    }

    private void ShowMainWindow()
    {
        if (_window is null) return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;
        _window.Focus();
    }

    private async Task ExitApplicationAsync()
    {
        if (_window is not null) await _window.PrepareExitAsync();
        _tray?.Dispose();
        _single?.Dispose();
        Shutdown();
    }
}
