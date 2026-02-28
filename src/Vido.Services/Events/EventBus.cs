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

    /// <summary>
    /// Registers a handler that will be invoked each time an event of type <typeparamref name="TEvent"/> is published.
    /// Returns an <see cref="IDisposable"/> that removes the subscription when disposed.
    /// </summary>
    /// <param name="handler">The callback to invoke when an event of type <typeparamref name="TEvent"/> is published.</param>
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

        return new EventBusSubscription(() => Unsubscribe(eventType, handler));
    }
    
    /// <summary>
    /// Dispatches the given event to all registered subscribers of type <typeparamref name="TEvent"/> on the calling thread.
    /// </summary>
    /// <param name="eventData">The event instance to deliver to all matching subscribers.</param>
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
}
