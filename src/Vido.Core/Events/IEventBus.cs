namespace Vido.Core.Events;

/// <summary>
/// Thread-safe publish/subscribe event bus.
/// Allows decoupled communication between application components.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Subscribes a handler for events of type <typeparamref name="TEvent"/>.
    /// Returns an <see cref="IDisposable"/> that removes the subscription when disposed.
    /// </summary>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;

    /// <summary>
    /// Publishes an event to all subscribers of type <typeparamref name="TEvent"/>.
    /// Handlers are invoked synchronously on the calling thread.
    /// </summary>
    void Publish<TEvent>(TEvent eventData) where TEvent : class;
}
