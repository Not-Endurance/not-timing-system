namespace Not.Events;

public interface IEventSubscriber
{
    Guid Subscribe(Func<Task> action);
    Guid Subscribe(Action action);
    Guid SubscribeAsync(Func<Task> action);
    Guid SubscribeAsync(Action action);
    void Unsubscribe(Guid guid);
}

public interface IEventSubscriber<T>
{
    Guid Subscribe(Func<T, Task> action);
    Guid Subscribe(Action<T> action);
    Guid SubscribeAsync(Func<T, Task> action);
    Guid SubscribeAsync(Action<T> action);
}
