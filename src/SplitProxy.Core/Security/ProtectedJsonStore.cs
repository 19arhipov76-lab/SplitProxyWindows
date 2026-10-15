using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SplitProxy.Core.Security;

public sealed class ProtectedJsonStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SplitProxy-Windows-v1");
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public T? Load<T>(string path)
    {
        if (!File.Exists(path)) return default;
        try
        {
            var protectedBytes = Convert.FromBase64String(File.ReadAllText(path, Encoding.UTF8));
            var plain = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<T>(plain, _json);
        }
        catch { return default; }
    }

    public void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var plain = JsonSerializer.SerializeToUtf8Bytes(value, _json);
        var protectedBytes = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        AtomicWrite(path, Convert.ToBase64String(protectedBytes));
        CryptographicOperations.ZeroMemory(plain);
    }

    public void Delete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void AtomicWrite(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, Encoding.UTF8);
        File.Move(tmp, path, true);
    }
}
