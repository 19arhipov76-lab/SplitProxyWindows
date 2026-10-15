using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace SplitProxy.App;

public sealed class TrayIconManager : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Window _window;
    private readonly Func<Task> _exit;

    public TrayIconManager(Window window, Func<Task> exit)
    {
        _window = window;
        _exit = exit;
        _icon = new Forms.NotifyIcon
        {
            Text = "Split Proxy",
            Icon = SystemIcons.Shield,
            Visible = true
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => ShowWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Выйти из приложения", null, async (_, _) => await _exit());
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => ShowWindow();
    }

    private void ShowWindow()
    {
        _window.Dispatcher.Invoke(() =>
        {
            _window.Show();
            if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
            _window.Activate();
        });
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
