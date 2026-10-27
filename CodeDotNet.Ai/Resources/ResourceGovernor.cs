using CodeDotNet.Ai.Abstractions;

namespace CodeDotNet.Ai.Resources;

/// <summary>
/// Allows one generation at a time. Embedding work waits while generation is active
/// (and vice versa for a single embedding batch), so the machine is never oversubscribed.
/// </summary>
public sealed class ResourceGovernor : IResourceGovernor
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public int ActiveCount { get; private set; }

    public async ValueTask<IAsyncDisposable> AcquireAsync(WorkKind kind, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        ActiveCount++;
        return new Lease(this);
    }

    private void Release()
    {
        ActiveCount--;
        _gate.Release();
    }

    private sealed class Lease(ResourceGovernor owner) : IAsyncDisposable
    {
        private int _disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
