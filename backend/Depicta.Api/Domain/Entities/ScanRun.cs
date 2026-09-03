using Depicta.Api.Domain.Enums;

namespace Depicta.Api.Domain.Entities;

/// <summary>One "Scan site" action: how many images were discovered and how it ended.</summary>
public class ScanRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SiteId { get; set; }
    public Site Site { get; set; } = null!;

    public ScanRunStatus Status { get; set; } = ScanRunStatus.Running;

    public int Discovered { get; set; }
    public int AlreadyHadAlt { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
