using Depicta.Api.Abstractions;
using Depicta.Api.Domain.Entities;
using Depicta.Api.Domain.Enums;
using Depicta.Api.Infrastructure;
using Depicta.Api.Vision;
using Microsoft.EntityFrameworkCore;

namespace Depicta.Api.Workers;

public sealed class WorkerOptions
{
    public int BatchSize { get; set; } = 20;
    public int PollSeconds { get; set; } = 10;
    public int DelayBetweenCallsMs { get; set; } = 400; // simple rate limiter

    /// <summary>How many times to try one image before giving up (transient errors only).</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Base for the exponential backoff between retries (800 → 1600 → 3200 ms …).</summary>
    public int RetryBaseMs { get; set; } = 800;
}

/// <summary>
/// Drains <see cref="MediaItemState.Unprocessed"/> items, calls the Vision provider for each,
/// and moves them to PendingReview (or Failed). This is the ONLY caller of the Vision
/// provider — approve/regenerate just change state and let this worker do the work.
///
/// Resilience: each image is retried with exponential backoff on transient failures
/// (network/timeout/HTTP 429/5xx) and fails fast on permanent ones; a failure on one image
/// never blocks the rest of the batch.
/// </summary>
public sealed class AltAnalysisWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly AnalysisSignal _signal;
    private readonly WorkerOptions _opts;
    private readonly ILogger<AltAnalysisWorker> _log;

    public AltAnalysisWorker(IServiceScopeFactory scopes, AnalysisSignal signal,
        WorkerOptions opts, ILogger<AltAnalysisWorker> log)
    {
        _scopes = scopes; _signal = signal; _opts = opts; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("AltAnalysisWorker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            int processed;
            try
            {
                processed = await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Batch processing error");
                processed = 0;
            }

            // If the batch was full there may be more; otherwise wait for a signal or poll.
            if (processed < _opts.BatchSize)
            {
                try { await _signal.WaitAsync(TimeSpan.FromSeconds(_opts.PollSeconds), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DepictaDbContext>();
        var vision = scope.ServiceProvider.GetRequiredService<IVisionProvider>();

        var batch = await db.MediaItems
            .Where(m => m.State == MediaItemState.Unprocessed)
            .OrderBy(m => m.CreatedAt)
            .Take(_opts.BatchSize)
            .ToListAsync(ct);

        if (batch.Count == 0) return 0;

        foreach (var item in batch)
        {
            item.MoveTo(MediaItemState.Processing);
        }
        await db.SaveChangesAsync(ct);

        foreach (var item in batch)
        {
            await ProcessOneAsync(db, vision, item, ct);

            if (_opts.DelayBetweenCallsMs > 0)
                await Task.Delay(_opts.DelayBetweenCallsMs, ct);
        }

        return batch.Count;
    }

    private async Task ProcessOneAsync(DepictaDbContext db, IVisionProvider vision, MediaItem item, CancellationToken ct)
    {
        AltSuggestion? suggestion = null;
        Exception? lastError = null;

        for (var attempt = 1; attempt <= Math.Max(1, _opts.MaxAttempts); attempt++)
        {
            try
            {
                suggestion = await vision.DescribeAsync(new VisionRequest(item.Url, item.Filename, null), ct);
                lastError = null;
                break;
            }
            catch (OperationCanceledException) { throw; } // worker is shutting down
            catch (VisionException ve)
            {
                lastError = ve;
                if (!ve.IsTransient || attempt >= _opts.MaxAttempts) break;

                var delay = ComputeDelay(ve.RetryAfter, attempt);
                _log.LogWarning(
                    "Vision transient error for media {MediaId} (attempt {Attempt}/{Max}, status {Status}); retrying in {Delay}ms",
                    item.Id, attempt, _opts.MaxAttempts, ve.StatusCode, (int)delay.TotalMilliseconds);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                // Unexpected, non-Vision error — don't spin on it.
                lastError = ex;
                break;
            }
        }

        if (suggestion is { } s)
        {
            var proposal = new AltProposal
            {
                MediaItemId = item.Id,
                ProposedAlt = s.Alt,
                Provider = vision.Key,
                Model = vision.Model,
                PromptVersion = ClaudeVisionProvider.PromptVersion,
                Confidence = s.Confidence,
                InputTokens = s.InputTokens,
                OutputTokens = s.OutputTokens,
            };
            db.AltProposals.Add(proposal);
            await db.SaveChangesAsync(ct); // persist to get proposal.Id
            item.ActiveProposalId = proposal.Id;
            item.MoveTo(MediaItemState.PendingReview);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            _log.LogWarning(lastError, "Vision failed for media {MediaId} after retries", item.Id);
            item.Fail($"Vision provider error: {lastError?.Message ?? "unknown error"}");
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Prefer a server-supplied Retry-After (capped), else exponential backoff + jitter.</summary>
    private TimeSpan ComputeDelay(TimeSpan? retryAfter, int attempt)
    {
        if (retryAfter is { } ra && ra > TimeSpan.Zero)
            return ra > TimeSpan.FromSeconds(60) ? TimeSpan.FromSeconds(60) : ra;

        var baseMs = Math.Max(1, _opts.RetryBaseMs);
        var exp = baseMs * Math.Pow(2, attempt - 1);
        var capped = Math.Min(exp, 30_000);
        var jitter = Random.Shared.NextDouble() * 0.3 * capped; // up to +30% to de-sync retries
        return TimeSpan.FromMilliseconds(capped + jitter);
    }
}
