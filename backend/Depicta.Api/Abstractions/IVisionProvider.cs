namespace Depicta.Api.Abstractions;

/// <summary>
/// Provider-agnostic Vision interface. The default implementation targets Claude Vision;
/// an OpenAI GPT-4o implementation can be dropped in without touching the pipeline.
/// Registered by key so the active provider is a config switch.
/// </summary>
public interface IVisionProvider
{
    /// <summary>Provider key, e.g. "claude" or "openai".</summary>
    string Key { get; }

    /// <summary>Concrete model id used, surfaced onto the proposal for reproducibility.</summary>
    string Model { get; }

    /// <summary>
    /// Generate a single WCAG-compliant alt string for an image. Implementations MUST
    /// enforce the output contract (<= 125 chars, no "image of/picture of" filler) and
    /// return strict, parsed JSON — see Phase 4 for the system prompt.
    /// </summary>
    Task<AltSuggestion> DescribeAsync(VisionRequest request, CancellationToken ct);
}

/// <param name="ImageUrl">Publicly reachable URL of the image (or a short-lived signed URL).</param>
/// <param name="Filename">Original filename — a weak hint, never the whole answer.</param>
/// <param name="SiteContext">Optional: site name / focus keyword to steer tone.</param>
public readonly record struct VisionRequest(string ImageUrl, string? Filename, string? SiteContext);

/// <param name="Alt">The proposed alt text, already trimmed to the contract.</param>
/// <param name="Confidence">0..1 model-reported confidence, if available.</param>
/// <param name="InputTokens">Usage, for cost tracking.</param>
/// <param name="OutputTokens">Usage, for cost tracking.</param>
public readonly record struct AltSuggestion(string Alt, double? Confidence, int? InputTokens, int? OutputTokens);
