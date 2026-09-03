namespace Depicta.Api.Workers;

/// <summary>
/// Lightweight wake signal so a scan can nudge the background worker to start processing
/// immediately instead of waiting for its next poll. Singleton.
/// </summary>
public sealed class AnalysisSignal
{
    private readonly SemaphoreSlim _sem = new(0, 1);

    /// <summary>Wake the worker (no-op if already signalled).</summary>
    public void Signal()
    {
        try { _sem.Release(); } catch (SemaphoreFullException) { /* already pending */ }
    }

    /// <summary>Wait for a signal or until <paramref name="timeout"/> elapses.</summary>
    public Task WaitAsync(TimeSpan timeout, CancellationToken ct) => _sem.WaitAsync(timeout, ct);
}
