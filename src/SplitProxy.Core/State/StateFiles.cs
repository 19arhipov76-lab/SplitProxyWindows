using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SplitProxy.Core.Models;
using SplitProxy.Core.Programs;

namespace SplitProxy.Core.State;

public static class StateFiles
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SplitProxy-Windows-MachineState-v1");

    public static ClientState LoadClientState()
    {
        try
        {
            if (!File.Exists(AppConstants.AppStatePath)) return new ClientState();
            var text = File.ReadAllText(AppConstants.AppStatePath, Encoding.UTF8);

            // v0.1.1+: machine-scoped DPAPI. Both the desktop app and LocalSystem service can read it.
            try
            {
                var protectedBytes = Convert.FromBase64String(text);
                var plain = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
                try
                {
                    return JsonSerializer.Deserialize<ClientState>(plain, Json) ?? new ClientState();
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(plain);
                }
            }
            catch (FormatException)
            {
                // Upgrade path from the early plaintext development build.
                var old = JsonSerializer.Deserialize<ClientState>(text, Json) ?? new ClientState();
                SaveClientState(old);
                return old;
            }
            catch (CryptographicException)
            {
                return new ClientState();
            }
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

        var plain = JsonSerializer.SerializeToUtf8Bytes(state, Json);
        try
        {
            var protectedBytes = ProtectedData.Protect(plain, Entropy, DataProtectionScope.LocalMachine);
            AtomicWrite(AppConstants.AppStatePath, Convert.ToBase64String(protectedBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
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
        File.WriteAllText(tmp, content, Encoding.UTF8);
        File.Move(tmp, path, true);
    }
}
