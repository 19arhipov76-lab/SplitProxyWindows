using System.Diagnostics;
using System.Text.Json;
using SplitProxy.Core;
using SplitProxy.Core.Models;
using SplitProxy.Core.Programs;

namespace SplitProxy.Service;

public sealed class RouteEngineManager
{
    private Process? _process;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private string _lastConfig = "";

    public bool ProcessExitedUnexpectedly => _process is { HasExited: true };

    public async Task StartOrReloadAsync(ClientState state, CancellationToken ct)
    {
        var proxy = state.Proxy ?? throw new InvalidOperationException("Proxy missing.");
        if (string.IsNullOrWhiteSpace(proxy.Username) || string.IsNullOrWhiteSpace(proxy.Password))
            throw new InvalidOperationException("SOCKS5 credentials missing.");

        var apps = state.Applications
            .Where(File.Exists)
            .Where(x => !BrowserGuard.IsForbidden(x))
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (apps.Length == 0) throw new InvalidOperationException("Нет разрешённых приложений для маршрутизации.");

        var engineDir = Path.Combine(AppContext.BaseDirectory, "engine");
        var workDir = Path.Combine(AppConstants.ServiceDataDirectory, "engine");
        Directory.CreateDirectory(workDir);
        var exe = Path.Combine(engineDir, "SplitProxy.RouteEngine.exe");
        if (!File.Exists(exe)) throw new FileNotFoundException("Не найден сетевой движок.", exe);

        // Credentials are NOT written to the route-engine config file. They are passed
        // only to the child process environment and live in memory of the service/engine.
        var config = new
        {
            proxy_host = proxy.Host,
            proxy_port = proxy.Port,
            intercept_tcp = true,
            intercept_udp = true,
            udp_session_timeout_sec = 60,
            apps,
            hostnames = Array.Empty<string>(),
            ips = Array.Empty<string>(),
            geoip = Array.Empty<string>()
        };
        var text = JsonSerializer.Serialize(config, _json);
        var fingerprint = text + "\n" + proxy.Username + "\n" + proxy.Password;
        var configPath = Path.Combine(workDir, "config.json");
        File.WriteAllText(configPath + ".tmp", text);
        File.Move(configPath + ".tmp", configPath, true);

        if (_process is { HasExited: false } && fingerprint == _lastConfig) return;
        await StopAsync();
        _lastConfig = fingerprint;

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = workDir,
            Arguments = $"\"{configPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.Environment["SPLITPROXY_SOCKS_USERNAME"] = proxy.Username;
        psi.Environment["SPLITPROXY_SOCKS_PASSWORD"] = proxy.Password;

        _process = Process.Start(psi) ?? throw new InvalidOperationException("Не удалось запустить сетевой движок.");
        _ = DrainAsync(_process.StandardOutput, Path.Combine(AppConstants.ServiceDataDirectory, "engine.log"), ct);
        _ = DrainAsync(_process.StandardError, Path.Combine(AppConstants.ServiceDataDirectory, "engine-error.log"), ct);
        await Task.Delay(700, ct);
        if (_process.HasExited)
            throw new InvalidOperationException($"Сетевой движок завершился с кодом {_process.ExitCode}. См. engine-error.log.");
    }

    public async Task StopAsync()
    {
        if (_process is null) return;
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        catch { }
        finally { _process.Dispose(); _process = null; }
    }

    private static async Task DrainAsync(StreamReader reader, string path, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null) break;
                await File.AppendAllTextAsync(path, $"{DateTimeOffset.Now:O} {line}{Environment.NewLine}", ct);
            }
        }
        catch { }
    }
}
