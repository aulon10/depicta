using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Depicta.Api.Abstractions;
using Depicta.Api.Domain.Entities;
using Depicta.Api.Security;

namespace Depicta.Api.Infrastructure.WordPress;

/// <summary>
/// The ONLY component that reaches a WordPress site. It calls the companion plugin's two
/// routes with Basic auth (Application Password) over HTTPS, and signs every request with an
/// HMAC-SHA256 over the method, route, timestamp, nonce and body. The shared secret itself is
/// never transmitted — the signature proves knowledge of it — and the timestamp + nonce make a
/// captured request useless to replay.
/// </summary>
public sealed class WordPressRestClient : IWordPressClient
{
    private const int PerPage = 100;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ICredentialProtector _protector;

    public WordPressRestClient(HttpClient http, ICredentialProtector protector)
    {
        _http = http;
        _protector = protector;
    }

    public async Task<IReadOnlyList<WpMedia>> GetMediaMissingAltAsync(Site site, CancellationToken ct)
    {
        var all = new List<WpMedia>();
        var page = 1;
        while (true)
        {
            using var req = Build(site, HttpMethod.Get, "/media/missing-alt", $"?page={page}&per_page={PerPage}", body: "");
            using var res = await _http.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();

            var body = await res.Content.ReadFromJsonAsync<MissingAltResponse>(Json, ct)
                       ?? throw new InvalidOperationException("Empty response from WordPress.");

            foreach (var i in body.Items ?? new())
                all.Add(new WpMedia(i.AttachmentId, i.Url ?? "", i.Filename, i.MimeType, i.Width, i.Height, i.CurrentAlt));

            if (all.Count >= body.Total || (body.Items?.Count ?? 0) == 0) break;
            page++;
            if (page > 1000) break; // hard safety stop
        }
        return all;
    }

    public async Task<WpAltWriteResult> SetAltAsync(Site site, long attachmentId, string alt, CancellationToken ct)
    {
        // Serialize once so the signature covers the exact bytes we send.
        var json = JsonSerializer.Serialize(new { alt });
        using var req = Build(site, HttpMethod.Post, $"/media/{attachmentId}/alt", query: "", body: json);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<SetAltResponse>(Json, ct)
                   ?? throw new InvalidOperationException("Empty response from WordPress.");
        return new WpAltWriteResult(body.Applied, body.PreviousAlt);
    }

    /// <param name="route">Route relative to the depicta/v1 namespace, e.g. "/media/123/alt". Signed.</param>
    /// <param name="query">Optional query string incl. leading '?'. NOT part of the signature.</param>
    /// <param name="body">Exact request body string ("" for GET). Signed via its SHA-256 hash.</param>
    private HttpRequestMessage Build(Site site, HttpMethod method, string route, string query, string body)
    {
        var baseUrl = site.Url.TrimEnd('/') + "/wp-json/depicta/v1";
        var req = new HttpRequestMessage(method, baseUrl + route + query);

        // Application Password (Basic auth) — WordPress authenticates the user from this.
        var appPassword = _protector.Unprotect(site.EncryptedAppPassword);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{site.WpUsername}:{appPassword}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // HMAC signature — proves we hold the shared secret without ever sending it.
        var secret = _protector.Unprotect(site.EncryptedPluginSecret);
        var (timestamp, nonce, signature) =
            RequestSigner.Sign(secret, method.Method, route, body, DateTimeOffset.UtcNow);

        req.Headers.Add("X-Depicta-Timestamp", timestamp);
        req.Headers.Add("X-Depicta-Nonce", nonce);
        req.Headers.Add("X-Depicta-Signature", signature);
        return req;
    }

    // --- response DTOs matching the plugin's JSON ---
    private sealed class MissingAltResponse
    {
        [JsonPropertyName("total")] public int Total { get; set; }
        [JsonPropertyName("items")] public List<WpMediaJson>? Items { get; set; }
    }
    private sealed class WpMediaJson
    {
        [JsonPropertyName("attachmentId")] public long AttachmentId { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("filename")] public string? Filename { get; set; }
        [JsonPropertyName("mimeType")] public string? MimeType { get; set; }
        [JsonPropertyName("width")] public int? Width { get; set; }
        [JsonPropertyName("height")] public int? Height { get; set; }
        [JsonPropertyName("currentAlt")] public string? CurrentAlt { get; set; }
    }
    private sealed class SetAltResponse
    {
        [JsonPropertyName("applied")] public bool Applied { get; set; }
        [JsonPropertyName("previousAlt")] public string? PreviousAlt { get; set; }
    }
}
