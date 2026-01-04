using System;

namespace Not.Events;

public abstract class EventBase<T>
{
    readonly object _lock = new();
    readonly Dictionary<Guid, T> _handlersByGuid = [];

    protected abstract void AddHandler(T handler);
    protected abstract void RemoveHandler(T handler);

    protected Guid InternalSubscribe(T handler)
    {
        lock (_lock)
        {
            var guid = Guid.NewGuid();
            _handlersByGuid.Add(guid, handler);
            AddHandler(handler);
            return guid;
        }
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

    public void Unsubscribe(Guid key)
    {
        lock (_lock)
        {
            if (!_handlersByGuid.TryGetValue(key, out var handler))
            {
                return;
            }
            _handlersByGuid.Remove(key);
            RemoveHandler(handler);
        }
    }

    public void UnsubscribeAll()
    {
        lock (_lock)
        {
            foreach (var (key, handler) in _handlersByGuid)
            {
                _handlersByGuid.Remove(key);
                RemoveHandler(handler);
            }
        }
    }
}
