using Vido.Core.Events;
using Vido.Services.Events;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Verifies the behavior of <see cref="EventBus"/>.
/// </summary>
public sealed class EventBusTests
{
    private readonly IEventBus _bus = new EventBus();

    // --- Test event types ---
    private sealed class TestEvent
    {
        public string Message { get; init; } = "";
    }

    private sealed class OtherEvent
    {
        public int Value { get; init; }
    }

    private static void HandleTestEvent(TestEvent _) { }

    /// <summary>
    /// Verifies that Publish delivers to subscriber.
    /// </summary>
    [Fact]
    public void Publish_DeliversToSubscriber()
    {
        TestEvent? received = null;
        _bus.Subscribe<TestEvent>(e => received = e);

        _bus.Publish(new TestEvent { Message = "hello" });

        Assert.NotNull(received);
        Assert.Equal("hello", received.Message);
    }

    /// <summary>
    /// Verifies that Publish delivers to multiple subscribers.
    /// </summary>
    [Fact]
    public void Publish_DeliversToMultipleSubscribers()
    {
        var calls = new List<string>();
        _bus.Subscribe<TestEvent>(e => calls.Add("A:" + e.Message));
        _bus.Subscribe<TestEvent>(e => calls.Add("B:" + e.Message));

        _bus.Publish(new TestEvent { Message = "x" });

        Assert.Equal(2, calls.Count);
        Assert.Contains("A:x", calls);
        Assert.Contains("B:x", calls);
    }

    /// <summary>
    /// Verifies that Publish does not deliver to unrelated subscriber.
    /// </summary>
    [Fact]
    public void Publish_DoesNotDeliverToUnrelatedSubscriber()
    {
        TestEvent? received = null;
        _bus.Subscribe<TestEvent>(e => received = e);

        _bus.Publish(new OtherEvent { Value = 42 });

        Assert.Null(received);
    }

    /// <summary>
    /// Verifies that Unsubscribe stops delivery.
    /// </summary>
    [Fact]
    public void Unsubscribe_StopsDelivery()
    {
        var count = 0;
        var sub = _bus.Subscribe<TestEvent>(_ => count++);

        _bus.Publish(new TestEvent());
        Assert.Equal(1, count);

        sub.Dispose();
        _bus.Publish(new TestEvent());
        Assert.Equal(1, count); // no second delivery
    }

    /// <summary>
    /// Verifies that Unsubscribe only affects disposed subscription.
    /// </summary>
    [Fact]
    public void Unsubscribe_OnlyAffectsDisposedSubscription()
    {
        var countA = 0;
        var countB = 0;
        var subA = _bus.Subscribe<TestEvent>(_ => countA++);
        _bus.Subscribe<TestEvent>(_ => countB++);

        subA.Dispose();
        _bus.Publish(new TestEvent());

        Assert.Equal(0, countA);
        Assert.Equal(1, countB);
    }

    /// <summary>
    /// Verifies that Publish with no subscribers does not throw.
    /// </summary>
    [Fact]
    public void Publish_WithNoSubscribers_DoesNotThrow()
    {
        var ex = Record.Exception(() => _bus.Publish(new TestEvent()));
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies that Double Dispose does not throw.
    /// </summary>
    [Fact]
    public void DoubleDispose_DoesNotThrow()
    {
        var sub = _bus.Subscribe<TestEvent>(_ => { });
        sub.Dispose();

        var ex = Record.Exception(() => sub.Dispose());
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies that Concurrent Publish And Subscribe does not throw.
    /// </summary>
    [Fact]
    public async Task ConcurrentPublishAndSubscribe_DoesNotThrow()
    {
        var count = 0;
        var tasks = new List<Task>();

        for (var i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var sub = _bus.Subscribe<TestEvent>(_ => Interlocked.Increment(ref count));
                _bus.Publish(new TestEvent());
                sub.Dispose();
            }));
        }

        await Task.WhenAll(tasks);
        // Just verifying no exceptions — count is non-deterministic
        Assert.True(count >= 0);
    }

    /// <summary>
    /// Verifies that disposing one duplicate handler subscription removes only one registration.
    /// </summary>
    [Fact]
    public void Unsubscribe_DuplicateHandler_RemovesSingleRegistration()
    {
        var count = 0;
        void Handler(TestEvent _) => count++;

        var sub1 = _bus.Subscribe<TestEvent>(Handler);
        _bus.Subscribe<TestEvent>(Handler);

        sub1.Dispose();
        _bus.Publish(new TestEvent());

        Assert.Equal(1, count);
    }

    /// <summary>
    /// Verifies that publishing with an existing subscriber does not allocate in the hot path.
    /// </summary>
    [Fact]
    public void Publish_WithSubscriber_DoesNotAllocate()
    {
        _bus.Subscribe<TestEvent>(HandleTestEvent);
        var evt = new TestEvent { Message = "alloc" };

        for (var i = 0; i < 128; i++)
            _bus.Publish(evt);

        _ = GC.GetAllocatedBytesForCurrentThread();
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 1024; i++)
            _bus.Publish(evt);

        var after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(0L, after - before);
    }
}