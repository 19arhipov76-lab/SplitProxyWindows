using System.Security.Cryptography;
using System.Text.Json;
using SplitProxy.Core.Api;
using SplitProxy.Core.Models;

namespace SplitProxy.Core.Updates;

public sealed class UpdateClient(ServerGateway gateway)
{
    public async Task<UpdateManifest> CheckAsync(Version current, CancellationToken ct = default)
    {
        using var doc = await gateway.GetVersionRawAsync(current, ct);
        var root = doc.RootElement;

        var latestText = String(root, "latest_version", "version", "latest") ?? current.ToString(3);
        _ = Version.TryParse(latestText.TrimStart('v','V'), out var latest);
        latest ??= current;

        var explicitFlag = Bool(root, "update_available", "has_update", "available");
        var updateAvailable = explicitFlag ?? latest > current;

        return new UpdateManifest
        {
            UpdateAvailable = updateAvailable,
            LatestVersion = latest,
            DownloadUrl = String(root, "download_url", "url", "installer_url"),
            Sha256 = String(root, "sha256", "checksum_sha256", "hash"),
            Mandatory = Bool(root, "mandatory", "required", "force") ?? false,
            Notes = String(root, "notes", "changelog", "message")
        };
    }

    public async Task<string> DownloadInstallerAsync(UpdateManifest manifest, IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(manifest.DownloadUrl))
            throw new InvalidOperationException("Сервер не указал ссылку на обновление.");
        var uri = new Uri(manifest.DownloadUrl);
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Обновление разрешено только по HTTPS.");

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        var dir = Path.Combine(Path.GetTempPath(), "SplitProxyUpdate");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"SplitProxySetup-{manifest.LatestVersion}.exe");

        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 128*1024, true);
        var buffer = new byte[128*1024];
        long done = 0;
        while (true)
        {
            var n = await input.ReadAsync(buffer, ct);
            if (n <= 0) break;
            await output.WriteAsync(buffer.AsMemory(0,n), ct);
            done += n;
            if (total is > 0) progress?.Report((int)(done * 100 / total.Value));
        }
        await output.FlushAsync(ct);

        if (string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            File.Delete(path);
            throw new CryptographicException("Сервер не вернул SHA-256 обновления.");
        }

        await using (var fs = File.OpenRead(path))
        {
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
            var expected = manifest.Sha256.Replace("sha256:", "", StringComparison.OrdinalIgnoreCase)
                .Replace(" ", "").Trim().ToLowerInvariant();
            if (expected.Length != 64 || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actual), Convert.FromHexString(expected)))
            {
                File.Delete(path);
                throw new CryptographicException("SHA-256 обновления не совпадает.");
            }
        }
        return path;
    }

    private static string? String(JsonElement root, params string[] names)
    {
        foreach (var n in names)
            if (root.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.GetString()))
                return p.GetString();
        return null;
    }
    private static bool? Bool(JsonElement root, params string[] names)
    {
        foreach (var n in names)
            if (root.TryGetProperty(n, out var p))
            {
                if (p.ValueKind == JsonValueKind.True) return true;
                if (p.ValueKind == JsonValueKind.False) return false;
            }
        return null;
    }
}
