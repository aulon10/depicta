using Depicta.Api.Domain.Enums;

namespace Depicta.Api.Contracts;

// ---- Authentication ----

/// <summary>Admin login credentials.</summary>
public record LoginRequest(string Username, string Password);

/// <summary>Issued access token and its expiry (UTC).</summary>
public record LoginResponse(string Token, DateTimeOffset ExpiresAt, string Username);

// ---- Site connection ----

/// <summary>Body for connecting the single WordPress site. The secret never comes back out.</summary>
public record ConnectSiteRequest(string Url, string WpUsername, string AppPassword, string PluginSecret);

public record SiteDto(Guid Id, string Url, string WpUsername, DateTimeOffset? LastScannedAt);

// ---- Scans ----

public record ScanRunDto(
    Guid Id, ScanRunStatus Status, int Discovered, int AlreadyHadAlt,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, string? Error);

// ---- Media / review queue ----

/// <summary>One row in the review UI: the original image next to its proposed alt.</summary>
public record MediaItemDto(
    Guid Id,
    long WpAttachmentId,
    string Url,
    string? Filename,
    int? Width,
    int? Height,
    string? CurrentAlt,
    MediaItemState State,
    ProposalDto? ActiveProposal,
    string? LastError,
    DateTimeOffset UpdatedAt);

public record ProposalDto(
    Guid Id, string ProposedAlt, string? EditedAlt, string Provider, string Model,
    double? Confidence, DateTimeOffset CreatedAt);

/// <summary>Paged list for the review queue.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

// ---- Actions (bulk-capable) ----

/// <summary>Approve one or more items. Optional per-item edited text overrides the proposal.</summary>
public record ApproveRequest(IReadOnlyList<ApproveItem> Items);
public record ApproveItem(Guid MediaItemId, string? EditedAlt);

public record RejectRequest(IReadOnlyList<Guid> MediaItemIds);
public record RegenerateRequest(IReadOnlyList<Guid> MediaItemIds);

/// <summary>Result of a bulk action: what succeeded and what didn't, per id.</summary>
public record BulkActionResult(IReadOnlyList<Guid> Succeeded, IReadOnlyList<BulkFailure> Failed);
public record BulkFailure(Guid MediaItemId, string Reason);
