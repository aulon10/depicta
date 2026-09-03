namespace Depicta.Api.Domain.Enums;

/// <summary>
/// Lifecycle of a single image that needs alt text. The core states mirror the product
/// workflow; <see cref="Failed"/> is an operational branch for a Vision/apply error that
/// the owner can retry from the UI.
/// </summary>
public enum MediaItemState
{
    /// <summary>Discovered by a scan, no proposal yet. Waiting for the worker.</summary>
    Unprocessed = 0,

    /// <summary>Worker is calling the Vision provider. Transient.</summary>
    Processing = 1,

    /// <summary>A proposal exists and is awaiting the owner's decision.</summary>
    PendingReview = 2,

    /// <summary>Owner approved; queued to be written to WordPress.</summary>
    Approved = 3,

    /// <summary>Successfully written to WordPress. Previous value stored for revert.</summary>
    Applied = 4,

    /// <summary>Owner rejected the proposal. No write happened.</summary>
    Rejected = 5,

    /// <summary>An applied value was rolled back to the previous alt text.</summary>
    Reverted = 6,

    /// <summary>Vision call or WP write failed. Retryable from the UI.</summary>
    Failed = 7
}
