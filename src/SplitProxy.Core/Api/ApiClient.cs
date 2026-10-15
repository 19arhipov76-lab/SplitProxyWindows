using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace SplitProxy.Core.Api;

public sealed class ApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ApiClient(HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.BaseAddress = new Uri(AppConstants.ApiBaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(15);
        var ver = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppConstants.UserAgentProduct}/{ver} Windows");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<JsonDocument> GetAsync(string path, string? bearer = null,
        IReadOnlyDictionary<string,string>? headers = null, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyAuthAndHeaders(request, bearer, headers);
        return await SendAsync(request, ct);
    }

    public async Task<JsonDocument> PostAsync(string path, object body, string? bearer = null,
        IReadOnlyDictionary<string,string>? headers = null, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json")
        };
        ApplyAuthAndHeaders(request, bearer, headers);
        return await SendAsync(request, ct);
    }

    private static void ApplyAuthAndHeaders(HttpRequestMessage request, string? bearer,
        IReadOnlyDictionary<string,string>? headers)
    {
        if (!string.IsNullOrWhiteSpace(bearer))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        if (headers is not null)
            foreach (var (key, value) in headers)
                request.Headers.TryAddWithoutValidation(key, value);
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new ApiException((int)response.StatusCode, ExtractMessage(text) ?? response.ReasonPhrase ?? "API error", text);
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
    }

    private static string? ExtractMessage(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                return m.GetString();
            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in errors.EnumerateObject())
                    if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                        return prop.Value[0].GetString();
            }
        }
        catch { }
        return null;
    }

    public void Dispose() => _http.Dispose();
}
