using FocusLock.Core.Abstractions;

namespace FocusLock.Tests.Fakes;

/// <summary>A timer that only fires when the test calls <see cref="Tick"/>.</summary>
public sealed class FakeTimer : ITickTimer
{
    private readonly List<Subscription> _subscriptions = [];

    public int ActiveCount => _subscriptions.Count(subscription => !subscription.Disposed);

    public TimeSpan? LastInterval { get; private set; }

    public IDisposable Start(TimeSpan interval, Action onTick)
    {
        LastInterval = interval;
        var subscription = new Subscription(onTick);
        _subscriptions.Add(subscription);
        return subscription;
    }

    public void Tick()
    {
        foreach (var subscription in _subscriptions.Where(subscription => !subscription.Disposed).ToList())
        {
            subscription.OnTick();
        }
    }

    public void Tick(int times)
    {
        for (var i = 0; i < times; i++)
        {
            Tick();
        }
    }

    private sealed class Subscription(Action onTick) : IDisposable
    {
        public Action OnTick { get; } = onTick;

        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
