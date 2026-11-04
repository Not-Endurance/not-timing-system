namespace Not.Events;

public class Event : EventBase<EventDelegate>, IEventSubscriber
{
    event EventDelegate? _delegate;

    protected override void AddHandler(EventDelegate handler)
    {
        _delegate += handler;
    }

    protected override void RemoveHandler(EventDelegate handler)
    {
        _delegate -= handler;
    }

    public void Emit()
    {
        _delegate?.Invoke();
    }

    public Guid Subscribe(Func<Task> action)
    {
        return InternalSubscribe(() => _ = RunSafely(action));
    }

    public Guid Subscribe(Action action)
    {
        return InternalSubscribe(() => RunSafely(action));
    }

    public Guid SubscribeAsync(Func<Task> action)
    {
        return InternalSubscribe(() => _ = Task.Run(() => RunSafely(action)));
    }

    public Guid SubscribeAsync(Action action)
    {
        return InternalSubscribe(() => _ = Task.Run(() => RunSafely(action)));
    }
}

public class Event<T> : EventBase<EventDelegate<T>>, IEventSubscriber<T>
{
    event EventDelegate<T>? _delegate;

    protected override void AddHandler(EventDelegate<T> handler)
    {
        _delegate += handler;
    }

    protected override void RemoveHandler(EventDelegate<T> handler)
    {
        _delegate -= handler;
    }

    public void Emit(T data)
    {
        _delegate?.Invoke(data);
    }

    public Guid Subscribe(Func<T, Task> action)
    {
        return InternalSubscribe(x => _ = RunSafely(() => action(x)));
    }

    public Guid Subscribe(Action<T> action)
    {
        return InternalSubscribe(x => RunSafely(() => action(x)));
    }

    public Guid SubscribeAsync(Func<T, Task> action)
    {
        return InternalSubscribe(x => _ = Task.Run(() => RunSafely(() => action(x))));
    }

    public Guid SubscribeAsync(Action<T> action)
    {
        return InternalSubscribe(x => _ = Task.Run(() => RunSafely(() => action(x))));
    }
}
