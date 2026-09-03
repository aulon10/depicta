using Depicta.Api.Domain.Enums;
using Depicta.Api.Domain.StateMachine;

namespace Depicta.Api.Domain.Entities;

/// <summary>
/// One image from the WordPress media library that needs (or needed) alt text.
/// A MediaItem is unique per (SiteId, WpAttachmentId). State changes go through
/// <see cref="MoveTo"/> so the state machine is always enforced.
/// </summary>
public class MediaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SiteId { get; set; }
    public Site Site { get; set; } = null!;

    /// <summary>WordPress attachment (post) ID.</summary>
    public required long WpAttachmentId { get; set; }

    public required string Url { get; set; }
    public string? Filename { get; set; }
    public string? MimeType { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }

    /// <summary>Alt text currently on the site, as seen at scan/apply time.</summary>
    public string? CurrentAlt { get; set; }

    public MediaItemState State { get; private set; } = MediaItemState.Unprocessed;

    /// <summary>Populated when State == Failed. Cleared on retry.</summary>
    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>All proposals ever generated (newest is the active one).</summary>
    public ICollection<AltProposal> Proposals { get; set; } = new List<AltProposal>();

    /// <summary>Apply/revert history for 1-click restoration and auditing.</summary>
    public ICollection<AltRevision> Revisions { get; set; } = new List<AltRevision>();

    /// <summary>The proposal currently shown/acted on. Null until analysis completes.</summary>
    public Guid? ActiveProposalId { get; set; }

    /// <summary>Enforced state change — throws on an illegal transition.</summary>
    public void MoveTo(MediaItemState next)
    {
        MediaItemStateMachine.EnsureTransition(State, next);
        State = next;
        UpdatedAt = DateTimeOffset.UtcNow;
        if (next != MediaItemState.Failed) LastError = null;
    }

    public void Fail(string error)
    {
        MediaItemStateMachine.EnsureTransition(State, MediaItemState.Failed);
        State = MediaItemState.Failed;
        LastError = error;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
