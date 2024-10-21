namespace Not.Events;
public abstract class EventBase<T>
{
    readonly Dictionary<Guid, T> _handlersByGuid = [];

    protected abstract void AddHandler(T handler);
    protected abstract void RemoveHandler(T handler);

    protected Guid InternalSubscribe(T handler)
    {
        var guid = Guid.NewGuid();
        _handlersByGuid.Add(guid, handler);
        AddHandler(handler);
        return guid;
    }

    protected Task ReturnCompletedTask(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    protected Task ReturnCompletedTask<TArgument>(Action<TArgument> action, TArgument argument)
    {
        action(argument);
        return Task.CompletedTask;
    }

    public void Unsubscribe(Guid guid)
    {
        if (!_handlersByGuid.TryGetValue(guid, out var handler))
        {
            return;
        }
        RemoveHandler(handler);
    }
}
