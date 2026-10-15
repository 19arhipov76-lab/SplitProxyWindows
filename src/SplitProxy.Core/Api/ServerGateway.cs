using System.Text.Json;
using SplitProxy.Core.Models;

namespace SplitProxy.Core.Api;

public sealed class ServerGateway(ApiClient client)
{
    public async Task<AccountSession> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        using var doc = await client.PostAsync("auth/login", new { email = email.Trim(), password }, ct: ct);
        var root = doc.RootElement;
        var token = ReadString(root, "token", "access_token", "account_token")
                    ?? throw new InvalidOperationException("Сервер не вернул токен учётной записи.");
        long? userId = null;
        if (root.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object &&
            user.TryGetProperty("id", out var id) && id.TryGetInt64(out var uid)) userId = uid;
        return new AccountSession(token, email.Trim(), userId, DateTimeOffset.UtcNow);
    }

    public async Task<AccountSession> RegisterAsync(string name, string email, string password, CancellationToken ct = default)
    {
        using var _ = await client.PostAsync("auth/register", new
        {
            name = name.Trim(), email = email.Trim(), password, password_confirmation = password
        }, ct: ct);
        return await LoginAsync(email, password, ct);
    }

    public async Task ValidateAccountAsync(string token, CancellationToken ct = default)
    {
        using var _ = await client.GetAsync("auth/me", token, ct: ct);
    }

    public async Task LogoutAsync(string token, CancellationToken ct = default)
    {
        try { using var _ = await client.PostAsync("auth/logout", new { }, token, ct: ct); }
        catch { }
    }

    public async Task<(string RequestCode, string InstallId)> RequestActivationAsync(
        DeviceIdentity identity, string deviceName, CancellationToken ct = default)
    {
        using var doc = await client.PostAsync("devices/activation/request", new
        {
            install_id = identity.InstallId,
            platform = AppConstants.Platform,
            device_name = deviceName,
            public_key = identity.PublicKeyPem
        }, ct: ct);
        var root = doc.RootElement;
        return (RequireString(root, "request_code"), RequireString(root, "install_id"));
    }

    public async Task<string> ApproveActivationAsync(string requestCode, string accountToken, CancellationToken ct = default)
    {
        using var doc = await client.PostAsync("devices/activation/approve",
            new { request_code = requestCode }, accountToken, ct: ct);
        return RequireString(doc.RootElement, "response_code");
    }

    public async Task<DeviceCredentials> FinishActivationAsync(
        string installId, string responseCode, string signatureBase64, string accountEmail,
        CancellationToken ct = default)
    {
        using var doc = await client.PostAsync("devices/activation/finish", new
        {
            install_id = installId,
            response_code = responseCode,
            signature = signatureBase64
        }, ct: ct);
        var root = doc.RootElement;
        return new DeviceCredentials(installId,
            RequireString(root, "key_id"),
            RequireString(root, "device_token"),
            accountEmail,
            DateTimeOffset.UtcNow);
    }


    public async Task<bool> AccountOwnsActiveInstallAsync(string installId, string accountToken, CancellationToken ct = default)
    {
        using var doc = await client.GetAsync("devices", accountToken, ct: ct);
        var root = doc.RootElement;
        if (!root.TryGetProperty("devices", out var devices) || devices.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Сервер не вернул список устройств аккаунта.");
        foreach (var device in devices.EnumerateArray())
        {
            if (device.TryGetProperty("install_id", out var iid) && iid.ValueKind == JsonValueKind.String &&
                string.Equals(iid.GetString(), installId, StringComparison.OrdinalIgnoreCase))
            {
                return device.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String &&
                       status.GetString()?.Equals("active", StringComparison.OrdinalIgnoreCase) == true;
            }
        }
        return false;
    }

    public async Task<DeviceProxyConfig> GetProxyConfigAsync(DeviceCredentials credentials, string country,
        CancellationToken ct = default)
    {
        var q = Uri.EscapeDataString(country.Trim().ToUpperInvariant());
        using var doc = await client.GetAsync($"devices/proxy/config?country={q}", credentials.DeviceToken,
            new Dictionary<string,string> { ["X-Device-Key"] = credentials.KeyId }, ct);
        return doc.RootElement.Deserialize<DeviceProxyConfig>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
               ?? throw new InvalidOperationException("Пустая конфигурация прокси.");
    }

    public async Task<JsonDocument> GetVersionRawAsync(Version current, CancellationToken ct = default)
    {
        var ver = Uri.EscapeDataString(current.ToString(3));
        return await client.GetAsync($"apps/version?platform=windows&version={ver}", ct: ct);
    }

    private static string RequireString(JsonElement root, string name)
        => root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.GetString())
            ? p.GetString()! : throw new InvalidOperationException($"Сервер не вернул {name}.");

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
            if (root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.GetString()))
                return p.GetString();
        return null;
    }
}
