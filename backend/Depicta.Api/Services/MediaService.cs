using Depicta.Api.Abstractions;
using Depicta.Api.Contracts;
using Depicta.Api.Domain.Entities;
using Depicta.Api.Domain.Enums;
using Depicta.Api.Infrastructure;
using Depicta.Api.Workers;
using Microsoft.EntityFrameworkCore;

namespace Depicta.Api.Services;

/// <summary>
/// The review queue and the human-in-the-loop actions. Approve is the only path that writes
/// to WordPress, and only for the items passed in. Every write records an AltRevision so it
/// can be reverted.
/// </summary>
public sealed class MediaService
{
    private readonly DepictaDbContext _db;
    private readonly IWordPressClient _wp;
    private readonly SiteService _sites;
    private readonly AnalysisSignal _signal;
    private readonly ILogger<MediaService> _log;

    public MediaService(DepictaDbContext db, IWordPressClient wp, SiteService sites,
        AnalysisSignal signal, ILogger<MediaService> log)
    {
        _db = db; _wp = wp; _sites = sites; _signal = signal; _log = log;
    }

    public async Task<PagedResult<MediaItemDto>> ListAsync(MediaItemState? state, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.MediaItems.AsNoTracking().Include(m => m.Proposals).AsQueryable();
        if (state is not null) q = q.Where(m => m.State == state);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(m => m.UpdatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<MediaItemDto>(items.Select(m => m.ToDto()).ToList(), total, page, pageSize);
    }

    public async Task<MediaItemDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await _db.MediaItems.AsNoTracking().Include(m => m.Proposals)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        return item?.ToDto();
    }

    public async Task<BulkActionResult> ApproveAsync(IReadOnlyList<ApproveItem> requested, CancellationToken ct)
    {
        var site = await _sites.RequireAsync(ct);
        var ids = requested.Select(r => r.MediaItemId).ToList();
        var edits = requested.ToDictionary(r => r.MediaItemId, r => r.EditedAlt);

        var items = await _db.MediaItems.Include(m => m.Proposals)
            .Where(m => ids.Contains(m.Id)).ToListAsync(ct);
        var byId = items.ToDictionary(m => m.Id);

        var ok = new List<Guid>();
        var failed = new List<BulkFailure>();

        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var item)) { failed.Add(new(id, "Not found")); continue; }
            if (item.State is not (MediaItemState.PendingReview or MediaItemState.Reverted))
            {
                failed.Add(new(id, $"Cannot approve from state '{item.State}'")); continue;
            }

            var proposal = item.ActiveProposalId is null
                ? null : item.Proposals.FirstOrDefault(p => p.Id == item.ActiveProposalId);

            var editedAlt = edits.TryGetValue(id, out var e) ? e?.Trim() : null;
            var effectiveAlt = !string.IsNullOrWhiteSpace(editedAlt) ? editedAlt : proposal?.ProposedAlt;

            if (string.IsNullOrWhiteSpace(effectiveAlt)) { failed.Add(new(id, "No alt text to apply")); continue; }
            if (proposal is not null && !string.IsNullOrWhiteSpace(editedAlt)) proposal.EditedAlt = editedAlt;

            item.MoveTo(MediaItemState.Approved);
            try
            {
                var res = await _wp.SetAltAsync(site, item.WpAttachmentId, effectiveAlt!, ct);
                _db.AltRevisions.Add(new AltRevision
                {
                    MediaItemId = item.Id,
                    AltProposalId = proposal?.Id,
                    PreviousAlt = res.PreviousAlt,
                    NewAlt = effectiveAlt!,
                });
                item.CurrentAlt = effectiveAlt;
                item.MoveTo(MediaItemState.Applied);
                ok.Add(id);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Apply failed for media {MediaId}", id);
                item.Fail($"WordPress write failed: {ex.Message}");
                failed.Add(new(id, "WordPress write failed"));
            }
        }

        await _db.SaveChangesAsync(ct);
        return new BulkActionResult(ok, failed);
    }

    public async Task<BulkActionResult> RejectAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var items = await _db.MediaItems.Where(m => ids.Contains(m.Id)).ToListAsync(ct);
        var byId = items.ToDictionary(m => m.Id);
        var ok = new List<Guid>(); var failed = new List<BulkFailure>();

        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var item)) { failed.Add(new(id, "Not found")); continue; }
            if (item.State != MediaItemState.PendingReview) { failed.Add(new(id, $"Cannot reject from '{item.State}'")); continue; }
            item.MoveTo(MediaItemState.Rejected);
            ok.Add(id);
        }
        await _db.SaveChangesAsync(ct);
        return new BulkActionResult(ok, failed);
    }

    public async Task<BulkActionResult> RegenerateAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var items = await _db.MediaItems.Where(m => ids.Contains(m.Id)).ToListAsync(ct);
        var byId = items.ToDictionary(m => m.Id);
        var ok = new List<Guid>(); var failed = new List<BulkFailure>();

        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var item)) { failed.Add(new(id, "Not found")); continue; }
            if (item.State is not (MediaItemState.PendingReview or MediaItemState.Rejected or MediaItemState.Failed))
            {
                failed.Add(new(id, $"Cannot regenerate from '{item.State}'")); continue;
            }
            item.ActiveProposalId = null;
            item.MoveTo(MediaItemState.Unprocessed);
            ok.Add(id);
        }
        await _db.SaveChangesAsync(ct);
        if (ok.Count > 0) _signal.Signal();
        return new BulkActionResult(ok, failed);
    }

    public async Task<MediaItemDto> RevertAsync(Guid id, CancellationToken ct)
    {
        var site = await _sites.RequireAsync(ct);
        var item = await _db.MediaItems.Include(m => m.Proposals).Include(m => m.Revisions)
            .FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new KeyNotFoundException("Media item not found");

        if (item.State != MediaItemState.Applied)
            throw new InvalidOperationException($"Cannot revert from state '{item.State}'");

        var lastApplied = item.Revisions
            .Where(r => r.RevertedAt is null)
            .OrderByDescending(r => r.AppliedAt).FirstOrDefault()
            ?? throw new InvalidOperationException("No applied revision to revert");

        var restore = lastApplied.PreviousAlt ?? "";
        await _wp.SetAltAsync(site, item.WpAttachmentId, restore, ct);

        lastApplied.RevertedAt = DateTimeOffset.UtcNow;
        item.CurrentAlt = restore;
        item.MoveTo(MediaItemState.Reverted);

        await _db.SaveChangesAsync(ct);
        return item.ToDto();
    }
}
