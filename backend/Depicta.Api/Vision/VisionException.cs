namespace Depicta.Api.Vision;

/// <summary>
/// A Vision-provider failure that carries enough context for the worker to decide whether to
/// retry. Transient failures (network hiccups, timeouts, HTTP 429/5xx) are worth retrying with
/// backoff; permanent ones (bad image, invalid key, model not found — HTTP 4xx except 408/429)
/// are not, so the item fails fast instead of burning retries.
/// </summary>
public sealed class VisionException : Exception
{
    public int? StatusCode { get; }
    public bool IsTransient { get; }

    /// <summary>Server-suggested wait before retrying (from a Retry-After header), if any.</summary>
    public TimeSpan? RetryAfter { get; }

    public VisionException(string message, bool isTransient, int? statusCode = null,
        TimeSpan? retryAfter = null, Exception? inner = null)
        : base(message, inner)
    {
        IsTransient = isTransient;
        StatusCode = statusCode;
        RetryAfter = retryAfter;
    }

    /// <summary>Classify an HTTP status from the Vision API as transient or permanent.</summary>
    public static bool IsTransientStatus(int status) =>
        status == 408 ||        // request timeout
        status == 429 ||        // rate limited
        status >= 500;          // server-side error
}
