using System.Text.Json;
using SplitProxy.Core.Models;
using SplitProxy.Core.Programs;

namespace SplitProxy.Core.State;

public static class StateFiles
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static ClientState LoadClientState()
    {
        try
        {
            if (!File.Exists(AppConstants.AppStatePath)) return new ClientState();
            return JsonSerializer.Deserialize<ClientState>(File.ReadAllText(AppConstants.AppStatePath), Json) ?? new ClientState();
        }
        catch { return new ClientState(); }
    }

    public static void SaveClientState(ClientState state)
    {
        Directory.CreateDirectory(AppConstants.MachineDataDirectory);
        state.Applications = state.Applications
            .Where(File.Exists)
            .Where(x => !BrowserGuard.IsForbidden(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        state.UpdatedAtUtc = DateTimeOffset.UtcNow;
        AtomicWrite(AppConstants.AppStatePath, JsonSerializer.Serialize(state, Json));
    }

    public static ServiceStatus LoadServiceStatus()
    {
        try
        {
            if (!File.Exists(AppConstants.ServiceStatusPath)) return new ServiceStatus();
            return JsonSerializer.Deserialize<ServiceStatus>(File.ReadAllText(AppConstants.ServiceStatusPath), Json) ?? new ServiceStatus();
        }
        catch { return new ServiceStatus(); }
    }

    public static void SaveServiceStatus(ServiceStatus status)
    {
        Directory.CreateDirectory(AppConstants.MachineDataDirectory);
        status.UpdatedAtUtc = DateTimeOffset.UtcNow;
        AtomicWrite(AppConstants.ServiceStatusPath, JsonSerializer.Serialize(status, Json));
    }

    private static void AtomicWrite(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, true);
    }
}
