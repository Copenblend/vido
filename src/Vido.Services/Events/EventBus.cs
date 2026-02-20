using System.Collections.Concurrent;
using Vido.Core.Events;

namespace Vido.Services.Events;

/// <summary>
/// Thread-safe publish/subscribe event bus using <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);

        _handlers.AddOrUpdate(
            eventType,
            _ => new List<Delegate> { handler },
            (_, list) =>
            {
                lock (_lock)
                {
                    list.Add(handler);
                }
                return list;
            });

        return new Subscription(() => Unsubscribe(eventType, handler));
    }

    public void Publish<TEvent>(TEvent eventData) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
            return;

        Delegate[] snapshot;
        lock (_lock)
        {
            snapshot = handlers.ToArray();
        }

        foreach (var handler in snapshot)
        {
            ((Action<TEvent>)handler)(eventData);
        }
    }

    private void Unsubscribe(Type eventType, Delegate handler)
    {
        if (!_handlers.TryGetValue(eventType, out var handlers))
            return;

        lock (_lock)
        {
            handlers.Remove(handler);
        }
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _onDispose, null)?.Invoke();
        }
    }
}
