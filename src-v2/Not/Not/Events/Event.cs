using Not.Safe;

namespace Not.Events;

public class Event : EventBase<EventDelegate>, IEventSubscriber
{
    event EventDelegate? _delegate;

    public void Emit()
    {
        _delegate?.Invoke();
    }

    public Guid Subscribe(Func<Task> action)
    {
        return InternalSubscribe(() => SafeHelper.Run(() => action()));
    }

    public Guid Subscribe(Action action)
    {
        return InternalSubscribe(() => SafeHelper.Run(() => ReturnCompletedTask(action)));
    }

    public Guid SubscribeAsync(Func<Task> action)
    {
        return InternalSubscribe(() => SafeHelper.RunAsync(() => action()));
    }

    public Guid SubscribeAsync(Action action)
    {
        return InternalSubscribe(() => SafeHelper.RunAsync(() => ReturnCompletedTask(action)));
    }

    protected override void AddHandler(EventDelegate handler)
    {
        _delegate += handler;
    }

    protected override void RemoveHandler(EventDelegate handler)
    {
        _delegate -= handler;
    }
}

public class Event<T> : EventBase<EventDelegate<T>>, IEventSubscriber<T>
{
    event EventDelegate<T>? _delegate;

    public void Emit(T data)
    {
        _delegate?.Invoke(data);
    }

    public Guid Subscribe(Func<T, Task> action)
    {
        return InternalSubscribe(x => SafeHelper.Run(() => action(x)));
    }

    public Guid Subscribe(Action<T> action)
    {
        return InternalSubscribe(x => SafeHelper.Run(() => ReturnCompletedTask(action, x)));
    }

    public Guid SubscribeAsync(Func<T, Task> action)
    {
        return InternalSubscribe(x => SafeHelper.RunAsync(() => action(x)));
    }

    public Guid SubscribeAsync(Action<T> action)
    {
        return InternalSubscribe(x => SafeHelper.RunAsync(() => ReturnCompletedTask(action, x)));
    }

    protected override void AddHandler(EventDelegate<T> handler)
    {
        _delegate += handler;
    }

    protected override void RemoveHandler(EventDelegate<T> handler)
    {
        _delegate -= handler;
    }
}