namespace Vido.Services.Events;

/// <summary>
/// Represents a disposable event bus subscription that unregisters its handler on dispose.
/// </summary>
internal sealed class EventBusSubscription(Action onDispose) : IDisposable
{
    private Action? _onDispose = onDispose;

    /// <summary>
    /// Disposes this subscription and unregisters the associated event handler exactly once.
    /// </summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _onDispose, null)?.Invoke();
    }
}
