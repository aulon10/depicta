using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Depicta.Api.Abstractions;
using ImageMagick;

namespace Depicta.Api.Vision;

public sealed class ClaudeVisionOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "claude-haiku-4-5-20251001";
    public string AnthropicVersion { get; set; } = "2023-06-01";
    public int MaxTokens { get; set; } = 200;

    /// <summary>Longest edge (px) sent to the model. Anthropic downscales beyond ~1568 anyway,
    /// so resizing here cuts upload size, tokens and cost with no quality loss that matters.</summary>
    public int MaxImageEdge { get; set; } = 1568;

    /// <summary>JPEG quality (1–100) used when the image is re-encoded.</summary>
    public int ImageQuality { get; set; } = 82;
}

/// <summary>
/// Default <see cref="IVisionProvider"/>: Anthropic Claude Vision. Downloads the image, right-sizes
/// it (resize + JPEG re-encode) to keep payloads small, applies the system prompt, parses the
/// strict-JSON reply, and runs it through <see cref="AltTextSanitizer"/>. Failures are surfaced as
/// <see cref="VisionException"/> so the worker can retry transient ones and fail fast on the rest.
/// </summary>
public sealed class ClaudeVisionProvider : IVisionProvider
{
    public const string PromptVersion = "v1";

    private static readonly string[] SupportedTypes = { "image/jpeg", "image/png", "image/gif", "image/webp" };

    private const string SystemPrompt = """
        You write alt text for images on a website, for SEO and web accessibility (WCAG 2.1).

        Return ONLY a JSON object: {"alt": string, "confidence": number}. No other text.

        Rules for the "alt" value:
        - Describe what the image shows and, if it has one, its function.
        - Be concise: 125 characters or fewer. One phrase, not a sentence with a period.
        - Do NOT begin with "image of", "picture of", "photo of", "graphic of" or similar.
        - Do not editorialize, guess names of people, or invent details you cannot see.
        - If the image contains text that carries the meaning, include that text.
        - If the image is purely decorative and adds no information, return "alt": "".
        - Write in the same language as the site (default: English).

        "confidence" is your own certainty about the description, from 0 to 1.
        """;

    private readonly HttpClient _http;
    private readonly ClaudeVisionOptions _opts;

    public ClaudeVisionProvider(HttpClient http, ClaudeVisionOptions opts)
    {
        _http = http;
        _opts = opts;
    }

    public string Key => "claude";
    public string Model => _opts.Model;

    public async Task<AltSuggestion> DescribeAsync(VisionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opts.ApiKey))
            throw new VisionException("Anthropic API key is not configured.", isTransient: false);

        var userText = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(request.Filename))
            userText.Append($"Filename hint (may be inaccurate): \"{request.Filename}\"\n");
        if (!string.IsNullOrWhiteSpace(request.SiteContext))
            userText.Append($"Site context (optional): \"{request.SiteContext}\"\n");
        userText.Append("Describe this image.");

        var (imgBytes, mediaType) = await FetchImageAsync(request.ImageUrl, ct);
        (imgBytes, mediaType) = PrepareImage(imgBytes, mediaType);
        var imgBase64 = Convert.ToBase64String(imgBytes);

        var payload = new
        {
            model = _opts.Model,
            max_tokens = _opts.MaxTokens,
            system = SystemPrompt,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "image", source = new { type = "base64", media_type = mediaType, data = imgBase64 } },
                        new { type = "text", text = userText.ToString() },
                    },
                },
            },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", _opts.ApiKey);
        req.Headers.Add("anthropic-version", _opts.AnthropicVersion);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        HttpResponseMessage res;
        try
        {
            res = await _http.SendAsync(req, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new VisionException("Anthropic request timed out.", isTransient: true, inner: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new VisionException($"Network error calling Anthropic: {ex.Message}", isTransient: true, inner: ex);
        }

        using (res)
        {
            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync(ct);
                var status = (int)res.StatusCode;
                var retryAfter = res.Headers.RetryAfter?.Delta
                    ?? (res.Headers.RetryAfter?.Date is DateTimeOffset at ? at - DateTimeOffset.UtcNow : (TimeSpan?)null);
                throw new VisionException(
                    $"Anthropic {status}: {Truncate(err)}",
                    isTransient: VisionException.IsTransientStatus(status),
                    statusCode: status,
                    retryAfter: retryAfter);
            }

            var body = await res.Content.ReadFromJsonSafeAsync(ct);
            var text = body?.Content?.FirstOrDefault(c => c.Type == "text")?.Text ?? "";

            var parsed = ParseStrictJson(text);
            var alt = AltTextSanitizer.Clean(parsed.Alt);

            return new AltSuggestion(
                alt,
                parsed.Confidence,
                body?.Usage?.InputTokens,
                body?.Usage?.OutputTokens);
        }
    }

    /// <summary>Download the image bytes. The model's servers cannot reach local/staging URLs
    /// (e.g. *.local) or auth-walled media, so the backend fetches the bytes itself.</summary>
    private async Task<(byte[] Bytes, string MediaType)> FetchImageAsync(string url, CancellationToken ct)
    {
        try
        {
            using var imgResp = await _http.GetAsync(url, ct);
            if (!imgResp.IsSuccessStatusCode)
            {
                var status = (int)imgResp.StatusCode;
                throw new VisionException(
                    $"Could not fetch image (HTTP {status}).",
                    isTransient: VisionException.IsTransientStatus(status),
                    statusCode: status);
            }
            var bytes = await imgResp.Content.ReadAsByteArrayAsync(ct);
            var mediaType = imgResp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return (bytes, mediaType);
        }
        catch (VisionException) { throw; }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new VisionException("Image download timed out.", isTransient: true, inner: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new VisionException($"Network error fetching image: {ex.Message}", isTransient: true, inner: ex);
        }
    }

    /// <summary>
    /// Right-size the image for the model: leave already-small, model-supported images untouched;
    /// otherwise resize the long edge down to <see cref="ClaudeVisionOptions.MaxImageEdge"/> and/or
    /// re-encode unsupported formats (AVIF, HEIC, TIFF, BMP, …) to JPEG.
    /// </summary>
    private (byte[] Bytes, string MediaType) PrepareImage(byte[] bytes, string mediaType)
    {
        var isSupported = SupportedTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase);
        var maxEdge = (uint)Math.Max(1, _opts.MaxImageEdge);

        uint longest = 0;
        try
        {
            var info = new MagickImageInfo(bytes);
            longest = Math.Max(info.Width, info.Height);
        }
        catch { /* header unreadable; fall through and let the full decode below decide */ }

        // Already fine as-is: supported format and within the size cap — send the original bytes.
        if (isSupported && longest != 0 && longest <= maxEdge)
            return (bytes, mediaType);

        try
        {
            using var image = new MagickImage(bytes);
            if (Math.Max(image.Width, image.Height) > maxEdge)
                image.Resize(new MagickGeometry(maxEdge, maxEdge)); // fits within the box, keeps aspect ratio
            image.Format = MagickFormat.Jpeg;
            image.Quality = (uint)Math.Clamp(_opts.ImageQuality, 1, 100);
            return (image.ToByteArray(), "image/jpeg");
        }
        catch (Exception ex)
        {
            // Unreadable/corrupt image, or a format Magick can't decode — retrying won't help.
            throw new VisionException($"Could not process image ({mediaType}): {ex.Message}",
                isTransient: false, inner: ex);
        }
    }

    private static string Truncate(string s, int max = 500) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";

    /// <summary>Parse {"alt","confidence"}, tolerating stray wrapping text just in case.</summary>
    private static (string? Alt, double? Confidence) ParseStrictJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return (text.Trim(), null);

        try
        {
            using var doc = JsonDocument.Parse(text.Substring(start, end - start + 1));
            var root = doc.RootElement;
            var alt = root.TryGetProperty("alt", out var a) ? a.GetString() : null;
            double? conf = root.TryGetProperty("confidence", out var c) && c.TryGetDouble(out var d) ? d : null;
            return (alt, conf);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}

// --- minimal response DTOs for the Anthropic Messages API ---

internal sealed class AnthropicResponse
{
    [JsonPropertyName("content")] public List<AnthropicContent>? Content { get; set; }
    [JsonPropertyName("usage")] public AnthropicUsage? Usage { get; set; }
}

internal sealed class AnthropicContent
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
}

internal sealed class AnthropicUsage
{
    [JsonPropertyName("input_tokens")] public int? InputTokens { get; set; }
    [JsonPropertyName("output_tokens")] public int? OutputTokens { get; set; }
}

internal static class HttpContentJsonExtensions
{
    public static async Task<AnthropicResponse?> ReadFromJsonSafeAsync(this HttpContent content, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<AnthropicResponse>(stream, cancellationToken: ct);
    }
}
