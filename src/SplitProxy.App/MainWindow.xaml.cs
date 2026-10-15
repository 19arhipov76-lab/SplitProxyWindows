using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

using SplitProxy.Core;
using SplitProxy.Core.Api;
using SplitProxy.Core.Devices;
using SplitProxy.Core.Models;
using SplitProxy.Core.Programs;
using SplitProxy.Core.Security;
using SplitProxy.Core.State;
using SplitProxy.Core.Updates;

namespace SplitProxy.App;

public partial class MainWindow : Window
{
    private readonly ApiClient _api = new();
    private readonly ServerGateway _gateway;
    private readonly SessionStore _store = new();
    private readonly DeviceActivationService _activation;
    private readonly UpdateClient _updates;
    private AccountSession? _session;
    private DeviceCredentials? _device;
    private DeviceProxyConfig? _proxyConfig;
    private ClientState _state;
    private bool _allowClose;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _suppressToggle;

    public event EventHandler? RequestHideToTray;

    public MainWindow()
    {
        InitializeComponent();
        _gateway = new ServerGateway(_api);
        _activation = new DeviceActivationService(_gateway, _store, new DeviceIdentityManager(_store));
        _updates = new UpdateClient(_gateway);
        _state = StateFiles.LoadClientState();
        ProgramsList.ItemsSource = _state.Applications;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await EnsureSignedInAsync();
        await RefreshConfigAsync(false);
        _ = RunUpdateLoopAsync(_lifetime.Token);
        _ = RunStatusLoopAsync(_lifetime.Token);
        _ = RunAuthorizationHeartbeatAsync(_lifetime.Token);
    }

    private async Task EnsureSignedInAsync()
    {
        _session = _store.LoadSession();
        if (_session is not null)
        {
            try { await _gateway.ValidateAccountAsync(_session.Token); }
            catch { _store.ClearSession(); _session = null; }
        }
        if (_session is null)
        {
            var login = new LoginWindow(_gateway) { Owner = this };
            if (login.ShowDialog() != true || login.Session is null)
            {
                MessageText.Text = "Для работы Split Proxy требуется вход в учётную запись.";
                SetRoutingEnabled(false, persist: true);
                return;
            }
            _session = login.Session;
            _store.SaveSession(_session);
        }
        AccountText.Text = _session.Email;
        _device = await _activation.EnsureActivatedAsync(_session);
        TouchAuthorization();
    }

    private async Task RefreshConfigAsync(bool showSuccess)
    {
        if (_session is null || _device is null) return;
        RefreshButton.IsEnabled = false;
        try
        {
            _proxyConfig = await _gateway.GetProxyConfigAsync(_device, _state.Country);
            var proxy = _proxyConfig.Proxy ?? throw new InvalidOperationException("Сервер не вернул proxy.");
            if (!proxy.Protocol.Equals("socks5", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Windows-клиент требует SOCKS5. Сервер вернул {proxy.Protocol}://{proxy.Server}:{proxy.Port}.");

            if (string.IsNullOrWhiteSpace(proxy.Username) || string.IsNullOrWhiteSpace(proxy.Password))
                throw new InvalidOperationException("Сервер не вернул данные авторизации SOCKS5.");

            _state.Proxy = new EngineProxyConfig
            {
                Host = proxy.Server,
                Port = proxy.Port,
                Protocol = "socks5",
                Username = proxy.Username,
                Password = proxy.Password
            };
            _state.Enabled = EnabledToggle.IsChecked == true || _state.Enabled;
            StateFiles.SaveClientState(_state);
            RouteText.Text = $"{proxy.EntryNode ?? "RF?"} {proxy.Server}:{proxy.Port} → {proxy.ExitCountry ?? _proxyConfig.ExitCountry ?? "?"}";
            if (showSuccess) MessageText.Text = "Конфигурация обновлена.";
        }
        catch (Exception ex)
        {
            MessageText.Text = "Ошибка конфигурации: " + ex.Message;
            _state.Enabled = false;
            StateFiles.SaveClientState(_state);
        }
        finally
        {
            _suppressToggle = true;
            EnabledToggle.IsChecked = _state.Enabled;
            _suppressToggle = false;
            RefreshButton.IsEnabled = true;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshConfigAsync(true);

    private void AddProgram_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null) { MessageText.Text = "Сначала войдите в учётную запись."; return; }
        var dlg = new OpenFileDialog { Filter = "Программы (*.exe)|*.exe", Multiselect = false };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            BrowserGuard.ThrowIfForbidden(dlg.FileName);
            if (!_state.Applications.Contains(dlg.FileName, StringComparer.OrdinalIgnoreCase))
                _state.Applications.Add(dlg.FileName);
            SavePrograms();
        }
        catch (Exception ex) { MessageText.Text = ex.Message; }
    }

    private void RemoveProgram_Click(object sender, RoutedEventArgs e)
    {
        if (ProgramsList.SelectedItem is string path) _state.Applications.Remove(path);
        SavePrograms();
    }

    private void SavePrograms()
    {
        StateFiles.SaveClientState(_state);
        ProgramsList.ItemsSource = null;
        ProgramsList.ItemsSource = _state.Applications;
        MessageText.Text = "Список программ сохранён.";
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        SetRoutingEnabled(false, persist: true);
        if (_session is not null) await _gateway.LogoutAsync(_session.Token);
        _store.ClearSession();
        _session = null;
        _state.AuthorizedProcessId = 0;
        _state.AuthorizedUntilUtc = null;
        AccountText.Text = "";
        await EnsureSignedInAsync();
        await RefreshConfigAsync(false);
    }

    private void Enabled_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressToggle) return;
        if (_session is null || _device is null || _state.Proxy is null || _state.Applications.Count == 0)
        {
            _suppressToggle = true; EnabledToggle.IsChecked = false; _suppressToggle = false;
            MessageText.Text = "Сначала войдите, получите конфигурацию и добавьте хотя бы одну программу.";
            return;
        }
        SetRoutingEnabled(true, persist: true);
    }

    private void Enabled_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressToggle) return;
        SetRoutingEnabled(false, persist: true);
    }

    private void SetRoutingEnabled(bool enabled, bool persist)
    {
        _state.Enabled = enabled;
        if (persist) StateFiles.SaveClientState(_state);
    }


    private void TouchAuthorization()
    {
        if (_session is null)
        {
            _state.AuthorizedProcessId = 0;
            _state.AuthorizedUntilUtc = null;
        }
        else
        {
            _state.AuthorizedProcessId = Environment.ProcessId;
            _state.AuthorizedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(3);
        }
        StateFiles.SaveClientState(_state);
    }

    private async Task RunAuthorizationHeartbeatAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_session is not null) TouchAuthorization();
            try { await Task.Delay(TimeSpan.FromSeconds(45), ct); } catch { break; }
        }
    }

    private async Task RunStatusLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var status = StateFiles.LoadServiceStatus();
            await Dispatcher.InvokeAsync(() =>
            {
                StatusText.Text = status.Running ? "Работает" : status.State switch { "error" => "Ошибка", _ => "Остановлен" };
                if (!string.IsNullOrWhiteSpace(status.Message)) MessageText.Text = status.Message;
            });
            try { await Task.Delay(TimeSpan.FromSeconds(2), ct); } catch { break; }
        }
    }

    private async Task RunUpdateLoopAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(12), ct).ContinueWith(_ => { }, TaskScheduler.Default);
        while (!ct.IsCancellationRequested)
        {
            try { await CheckForUpdateAsync(ct); } catch { }
            try { await Task.Delay(TimeSpan.FromHours(2), ct); } catch { break; }
        }
    }

    private async Task CheckForUpdateAsync(CancellationToken ct)
    {
        var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0,1,0);
        var manifest = await _updates.CheckAsync(current, ct);
        if (!manifest.UpdateAvailable || manifest.LatestVersion <= current) return;

        var answer = await Dispatcher.InvokeAsync(() => MessageBox.Show(this,
            $"Доступна новая версия Split Proxy {manifest.LatestVersion}.\n\nСкачать и установить сейчас?",
            "Обновление Split Proxy", MessageBoxButton.YesNo, MessageBoxImage.Information));
        if (answer != MessageBoxResult.Yes) return;

        MessageText.Text = "Скачивание обновления...";
        var installer = await _updates.DownloadInstallerAsync(manifest, null, ct);
        LaunchUpdater(installer);
    }

    private void LaunchUpdater(string installer)
    {
        var installedUpdater = Path.Combine(AppContext.BaseDirectory, "SplitProxy.Updater.exe");
        if (!File.Exists(installedUpdater)) throw new FileNotFoundException("Не найден SplitProxy.Updater.exe", installedUpdater);
        var tempUpdater = Path.Combine(Path.GetTempPath(), $"SplitProxy.Updater.{Guid.NewGuid():N}.exe");
        File.Copy(installedUpdater, tempUpdater, true);
        var appPath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "SplitProxy.exe");
        Process.Start(new ProcessStartInfo
        {
            FileName = tempUpdater,
            UseShellExecute = true,
            Arguments = $"--pid {Environment.ProcessId} --installer \"{installer}\" --app \"{appPath}\""
        });
        _allowClose = true;
        Application.Current.Shutdown();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        RequestHideToTray?.Invoke(this, EventArgs.Empty);
    }

    public Task PrepareExitAsync()
    {
        _allowClose = true;
        _lifetime.Cancel();
        _state.AuthorizedProcessId = 0;
        _state.AuthorizedUntilUtc = null;
        SetRoutingEnabled(false, persist: true);
        return Task.CompletedTask;
    }
}
