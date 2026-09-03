using Depicta.Api.Domain.Entities;

namespace Depicta.Api.Abstractions;

/// <summary>
/// Thin client over the Depicta companion plugin's two REST routes. This is the ONLY thing
/// in the system that can reach a WordPress site, and it can do exactly two things.
/// </summary>
public interface IWordPressClient
{
    /// <summary>GET /depicta/v1/media/missing-alt — images with empty alt text.</summary>
    Task<IReadOnlyList<WpMedia>> GetMediaMissingAltAsync(Site site, CancellationToken ct);

    /// <summary>
    /// POST /depicta/v1/media/{attachmentId}/alt — set alt for one attachment.
    /// The plugin returns the value that was there before, which we persist for revert.
    /// </summary>
    Task<WpAltWriteResult> SetAltAsync(Site site, long attachmentId, string alt, CancellationToken ct);
}

public readonly record struct WpMedia(
    long AttachmentId, string Url, string? Filename, string? MimeType, int? Width, int? Height, string? CurrentAlt);

public readonly record struct WpAltWriteResult(bool Applied, string? PreviousAlt);
