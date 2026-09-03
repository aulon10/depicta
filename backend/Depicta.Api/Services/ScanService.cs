using Depicta.Api.Abstractions;
using Depicta.Api.Contracts;
using Depicta.Api.Domain.Entities;
using Depicta.Api.Domain.Enums;
using Depicta.Api.Infrastructure;
using Depicta.Api.Workers;
using Microsoft.EntityFrameworkCore;

namespace Depicta.Api.Services;

/// <summary>
/// Runs a scan: pulls images missing alt from WordPress and upserts them as MediaItems in
/// <see cref="MediaItemState.Unprocessed"/>, then wakes the analysis worker.
/// </summary>
public sealed class ScanService
{
    private readonly DepictaDbContext _db;
    private readonly IWordPressClient _wp;
    private readonly SiteService _sites;
    private readonly AnalysisSignal _signal;
    private readonly ILogger<ScanService> _log;

    public ScanService(DepictaDbContext db, IWordPressClient wp, SiteService sites,
        AnalysisSignal signal, ILogger<ScanService> log)
    {
        _db = db; _wp = wp; _sites = sites; _signal = signal; _log = log;
    }

    public async Task<ScanRunDto> StartAsync(CancellationToken ct)
    {
        var site = await _sites.RequireAsync(ct);

        var run = new ScanRun { SiteId = site.Id, Status = ScanRunStatus.Running };
        _db.ScanRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        try
        {
            var media = await _wp.GetMediaMissingAltAsync(site, ct);
            var ids = media.Select(m => m.AttachmentId).ToList();

            var existing = await _db.MediaItems
                .Where(m => m.SiteId == site.Id && ids.Contains(m.WpAttachmentId))
                .ToDictionaryAsync(m => m.WpAttachmentId, ct);

            var queued = 0;
            foreach (var wm in media)
            {
                if (existing.TryGetValue(wm.AttachmentId, out var item))
                {
                    item.Url = wm.Url;
                    item.Filename = wm.Filename;
                    item.MimeType = wm.MimeType;
                    item.Width = wm.Width;
                    item.Height = wm.Height;
                    item.CurrentAlt = wm.CurrentAlt;
                    if (item.State == MediaItemState.Failed) { item.MoveTo(MediaItemState.Unprocessed); queued++; }
                }
                else
                {
                    _db.MediaItems.Add(new MediaItem
                    {
                        SiteId = site.Id,
                        WpAttachmentId = wm.AttachmentId,
                        Url = wm.Url,
                        Filename = wm.Filename,
                        MimeType = wm.MimeType,
                        Width = wm.Width,
                        Height = wm.Height,
                        CurrentAlt = wm.CurrentAlt,
                    });
                    queued++;
                }
            }

            run.Discovered = queued;
            run.Status = ScanRunStatus.Completed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            site.LastScannedAt = run.CompletedAt;

            await _db.SaveChangesAsync(ct);
            if (queued > 0) _signal.Signal();

            return run.ToDto();
        }
        catch (Exception ex) when (ex is not SiteNotConnectedException)
        {
            _log.LogError(ex, "Scan failed for site {SiteId}", site.Id);
            run.Status = ScanRunStatus.Failed;
            run.Error = ex.Message;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw new ScanFailedException(ex.Message, ex);
        }
    }

    public async Task<IReadOnlyList<ScanRunDto>> ListAsync(int take, CancellationToken ct) =>
        await _db.ScanRuns.AsNoTracking()
            .OrderByDescending(r => r.StartedAt).Take(Math.Clamp(take, 1, 100))
            .Select(r => r.ToDto()).ToListAsync(ct);

    public async Task<ScanRunDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var run = await _db.ScanRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return run?.ToDto();
    }
}

public sealed class ScanFailedException : Exception
{
    public ScanFailedException(string message, Exception inner) : base(message, inner) { }
}
