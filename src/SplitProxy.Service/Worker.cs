using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using SplitProxy.Core.Models;
using SplitProxy.Core.State;

namespace SplitProxy.Service;

public sealed class Worker(ILogger<Worker> log, RouteEngineManager engine) : BackgroundService
{
    private string _lastFingerprint = "";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Split Proxy service started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var state = StateFiles.LoadClientState();
                var fp = Fingerprint(state);
                if (fp != _lastFingerprint)
                {
                    _lastFingerprint = fp;
                    await ApplyAsync(state, stoppingToken);
                }
                if (engine.ProcessExitedUnexpectedly)
                    await ApplyAsync(state, stoppingToken);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Service loop error");
                StateFiles.SaveServiceStatus(new ServiceStatus { State = "error", Message = ex.Message });
            }

            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); } catch { break; }
        }
        await engine.StopAsync();
    }

    private async Task ApplyAsync(ClientState state, CancellationToken ct)
    {
        if (!state.Enabled || !IsAuthorized(state))
        {
            await engine.StopAsync();
            StateFiles.SaveServiceStatus(new ServiceStatus { Running = false, State = "stopped", Message = null });
            return;
        }
        if (state.Proxy is null || state.Applications.Count == 0)
        {
            await engine.StopAsync();
            StateFiles.SaveServiceStatus(new ServiceStatus { Running = false, State = "error", Message = "Нет прокси или выбранных программ." });
            return;
        }
        if (!state.Proxy.Protocol.Equals("socks5", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Маршрутизатор Windows работает только через SOCKS5.");

        await engine.StartOrReloadAsync(state, ct);
        StateFiles.SaveServiceStatus(new ServiceStatus
        {
            Running = true,
            State = "running",
            ProxyEndpoint = $"{state.Proxy.Host}:{state.Proxy.Port}",
            Message = null
        });
    }

    private static bool IsAuthorized(ClientState state)
    {
        if (state.AuthorizedProcessId <= 0 || state.AuthorizedUntilUtc is null || state.AuthorizedUntilUtc <= DateTimeOffset.UtcNow)
            return false;
        try
        {
            using var p = Process.GetProcessById(state.AuthorizedProcessId);
            return !p.HasExited && p.ProcessName.Equals("SplitProxy", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static string Fingerprint(ClientState state)
    {
        var apps = string.Join('|', state.Applications.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        return $"{state.Enabled}|{IsAuthorized(state)}|{state.AuthorizedProcessId}|{state.Proxy?.Host}|{state.Proxy?.Port}|{state.Proxy?.Protocol}|{apps}";
    }
}
