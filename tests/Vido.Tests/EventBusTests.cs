using Vido.Core.Events;
using Vido.Services.Events;
using Xunit;

namespace Vido.Tests;

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

    [Fact]
    public void Publish_DeliversToSubscriber()
    {
        TestEvent? received = null;
        _bus.Subscribe<TestEvent>(e => received = e);

        _bus.Publish(new TestEvent { Message = "hello" });

        Assert.NotNull(received);
        Assert.Equal("hello", received.Message);
    }

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

    [Fact]
    public void Publish_DoesNotDeliverToUnrelatedSubscriber()
    {
        TestEvent? received = null;
        _bus.Subscribe<TestEvent>(e => received = e);

        _bus.Publish(new OtherEvent { Value = 42 });

        Assert.Null(received);
    }

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

    [Fact]
    public void Publish_WithNoSubscribers_DoesNotThrow()
    {
        var ex = Record.Exception(() => _bus.Publish(new TestEvent()));
        Assert.Null(ex);
    }

    [Fact]
    public void DoubleDispose_DoesNotThrow()
    {
        var sub = _bus.Subscribe<TestEvent>(_ => { });
        sub.Dispose();

        var ex = Record.Exception(() => sub.Dispose());
        Assert.Null(ex);
    }

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
}
