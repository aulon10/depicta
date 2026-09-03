using Depicta.Api.Domain.Enums;

namespace Depicta.Api.Domain.StateMachine;

/// <summary>
/// Single source of truth for legal <see cref="MediaItemState"/> transitions.
/// Every state change in the app must go through <see cref="CanTransition"/> /
/// <see cref="EnsureTransition"/> so an image can never, for example, jump from
/// Unprocessed straight to Applied and skip the owner's approval.
///
/// Regenerate / retry all funnel back to <see cref="MediaItemState.Unprocessed"/> so the
/// background worker is the ONLY thing that ever calls the Vision provider.
/// </summary>
public static class MediaItemStateMachine
{
    private static readonly IReadOnlyDictionary<MediaItemState, MediaItemState[]> Allowed =
        new Dictionary<MediaItemState, MediaItemState[]>
        {
            [MediaItemState.Unprocessed]   = new[] { MediaItemState.Processing },
            [MediaItemState.Processing]    = new[] { MediaItemState.PendingReview, MediaItemState.Failed },
            [MediaItemState.PendingReview] = new[] { MediaItemState.Approved, MediaItemState.Rejected, MediaItemState.Unprocessed },
            [MediaItemState.Approved]      = new[] { MediaItemState.Applied, MediaItemState.Failed },
            [MediaItemState.Applied]       = new[] { MediaItemState.Reverted },
            [MediaItemState.Rejected]      = new[] { MediaItemState.Unprocessed }, // regenerate
            [MediaItemState.Reverted]      = new[] { MediaItemState.Approved },    // re-apply
            [MediaItemState.Failed]        = new[] { MediaItemState.Unprocessed }, // retry
        };

    public static bool CanTransition(MediaItemState from, MediaItemState to) =>
        Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;

    /// <summary>Throws <see cref="InvalidStateTransitionException"/> if the move is illegal.</summary>
    public static void EnsureTransition(MediaItemState from, MediaItemState to)
    {
        if (!CanTransition(from, to))
            throw new InvalidStateTransitionException(from, to);
    }
}

public sealed class InvalidStateTransitionException : Exception
{
    public InvalidStateTransitionException(MediaItemState from, MediaItemState to)
        : base($"Illegal MediaItem transition: {from} -> {to}.") { }
}
