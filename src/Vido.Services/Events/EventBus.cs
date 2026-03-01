using System.Collections.Concurrent;
using Vido.Core.Events;

namespace Vido.Services.Events;

/// <summary>
/// Thread-safe publish/subscribe event bus using <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, Delegate[]> _handlers = new();
    private readonly object _writeLock = new();

    /// <summary>
    /// Registers a handler that will be invoked each time an event of type <typeparamref name="TEvent"/> is published.
    /// Returns an <see cref="IDisposable"/> that removes the subscription when disposed.
    /// </summary>
    /// <param name="handler">The callback to invoke when an event of type <typeparamref name="TEvent"/> is published.</param>
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);

        lock (_writeLock)
        {
            _handlers.AddOrUpdate(
                eventType,
                _ => new Delegate[] { handler },
                (_, existing) =>
                {
                    var next = new Delegate[existing.Length + 1];
                    Array.Copy(existing, next, existing.Length);
                    next[existing.Length] = handler;
                    return next;
                });
        }

        return new EventBusSubscription(() => Unsubscribe(eventType, handler));
    }
    
    /// <summary>
    /// Dispatches the given event to all registered subscribers of type <typeparamref name="TEvent"/> on the calling thread.
    /// </summary>
    /// <param name="eventData">The event instance to deliver to all matching subscribers.</param>
    public void Publish<TEvent>(TEvent eventData)
    {
        if (eventData is null)
            throw new ArgumentNullException(nameof(eventData));

        if (!_handlers.TryGetValue(typeof(TEvent), out var snapshot))
            return;

        var handlers = Volatile.Read(ref snapshot);
        for (var i = 0; i < handlers.Length; i++)
        {
            ((Action<TEvent>)handlers[i]).Invoke(eventData);
        }
    }

    private void Unsubscribe(Type eventType, Delegate handler)
    {
        lock (_writeLock)
        {
            if (!_handlers.TryGetValue(eventType, out var existing))
                return;

            var index = Array.IndexOf(existing, handler);
            if (index < 0)
                return;

            if (existing.Length == 1)
            {
                _handlers.TryRemove(eventType, out _);
                return;
            }

            var next = new Delegate[existing.Length - 1];
            if (index > 0)
                Array.Copy(existing, 0, next, 0, index);

            if (index < existing.Length - 1)
                Array.Copy(existing, index + 1, next, index, existing.Length - index - 1);

            _handlers[eventType] = next;
        }
    }
}
