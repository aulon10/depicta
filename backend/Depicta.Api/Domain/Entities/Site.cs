namespace Depicta.Api.Domain.Entities;

/// <summary>
/// The connected WordPress site. v1 holds exactly one row, but modelling it as an entity
/// means going multi-tenant later is a schema addition (users, memberships) rather than a
/// rewrite — every downstream table already has a SiteId.
/// </summary>
public class Site
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Base URL of the site, e.g. https://example.com</summary>
    public required string Url { get; set; }

    /// <summary>WP username the Application Password belongs to.</summary>
    public required string WpUsername { get; set; }

    /// <summary>
    /// Application Password, encrypted at rest (ASP.NET Data Protection / KMS).
    /// Never returned by any API and never logged.
    /// </summary>
    public required byte[] EncryptedAppPassword { get; set; }

    /// <summary>Per-site shared secret sent as a header to the plugin, encrypted at rest.</summary>
    public required byte[] EncryptedPluginSecret { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastScannedAt { get; set; }

    public ICollection<MediaItem> MediaItems { get; set; } = new List<MediaItem>();
    public ICollection<ScanRun> ScanRuns { get; set; } = new List<ScanRun>();
}
