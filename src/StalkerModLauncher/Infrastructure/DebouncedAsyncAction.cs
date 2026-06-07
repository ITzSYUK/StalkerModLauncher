namespace StalkerModLauncher.Infrastructure;

public sealed class DebouncedAsyncAction : IDisposable
{
    private readonly Func<Task> _action;
    private readonly TimeSpan _delay;
    private readonly object _sync = new();
    private CancellationTokenSource? _cancellation;

    public DebouncedAsyncAction(Func<Task> action, TimeSpan delay)
    {
        _action = action;
        _delay = delay;
    }

    public void Schedule()
    {
        CancellationToken token;
        lock (_sync)
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource();
            token = _cancellation.Token;
        }

        _ = RunAsync(token);
    }

    public void Cancel()
    {
        lock (_sync)
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_delay, cancellationToken);
            await _action();
        }
        catch (OperationCanceledException)
        {
            // A newer scheduled action superseded this one.
        }
    }

    public void Dispose()
    {
        Cancel();
    }
}
