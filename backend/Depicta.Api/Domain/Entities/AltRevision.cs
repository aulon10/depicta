namespace Depicta.Api.Domain.Entities;

/// <summary>
/// An audit + undo record: every time Depicta writes alt text to WordPress it stores the
/// value that was there before. Revert simply writes <see cref="PreviousAlt"/> back.
/// </summary>
public class AltRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MediaItemId { get; set; }
    public MediaItem MediaItem { get; set; } = null!;

    /// <summary>The proposal that was applied (null if a manual/edited value).</summary>
    public Guid? AltProposalId { get; set; }

    /// <summary>Alt text on the site immediately before this write (may be empty).</summary>
    public string? PreviousAlt { get; set; }

    /// <summary>Alt text this revision wrote.</summary>
    public required string NewAlt { get; set; }

    public DateTimeOffset AppliedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Set when this specific write was later rolled back.</summary>
    public DateTimeOffset? RevertedAt { get; set; }
}
