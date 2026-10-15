using System.Text.Json.Serialization;

namespace SplitProxy.Core.Models;

public sealed record AccountSession(string Token, string Email, long? UserId, DateTimeOffset SavedAtUtc);

public sealed record DeviceIdentity(
    string InstallId,
    string PublicKeyPem,
    string ProtectedPrivateKeyBase64,
    DateTimeOffset CreatedAtUtc);

public sealed record DeviceCredentials(
    string InstallId,
    string KeyId,
    string DeviceToken,
    string AccountEmail,
    DateTimeOffset SavedAtUtc);

public sealed record ProxyRoute(
    [property: JsonPropertyName("server")] string Server,
    [property: JsonPropertyName("port")] int Port,
    [property: JsonPropertyName("protocol")] string Protocol,
    [property: JsonPropertyName("exit_country")] string? ExitCountry,
    [property: JsonPropertyName("entry_node")] string? EntryNode,
    [property: JsonPropertyName("exit_node")] string? ExitNode,
    [property: JsonPropertyName("checked_at")] string? CheckedAt,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("password")] string? Password);

public sealed class DeviceProxyConfig
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("mode")] public string? Mode { get; set; }
    [JsonPropertyName("platform")] public string? Platform { get; set; }
    [JsonPropertyName("install_id")] public string? InstallId { get; set; }
    [JsonPropertyName("plan")] public string? Plan { get; set; }
    [JsonPropertyName("exit_country")] public string? ExitCountry { get; set; }
    [JsonPropertyName("proxy")] public ProxyRoute? Proxy { get; set; }
    [JsonPropertyName("available_exit_countries")] public List<string>? AvailableExitCountries { get; set; }
}

public sealed class UpdateManifest
{
    public bool UpdateAvailable { get; init; }
    public Version LatestVersion { get; init; } = new(0,0,0);
    public string? DownloadUrl { get; init; }
    public string? Sha256 { get; init; }
    public bool Mandatory { get; init; }
    public string? Notes { get; init; }
}

public sealed class ClientState
{
    public bool Enabled { get; set; }
    public string Country { get; set; } = "NL";
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int AuthorizedProcessId { get; set; }
    public DateTimeOffset? AuthorizedUntilUtc { get; set; }
    public List<string> Applications { get; set; } = [];
    public EngineProxyConfig? Proxy { get; set; }
}

public sealed class EngineProxyConfig
{
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Protocol { get; set; } = "socks5";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class ServiceStatus
{
    public bool Running { get; set; }
    public string State { get; set; } = "stopped";
    public string? Message { get; set; }
    public string? ExitCountry { get; set; }
    public string? ProxyEndpoint { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
