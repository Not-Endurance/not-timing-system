using Not.Events;

namespace Not.Notify;

public interface INotifier
{
    void Inform(string message);
    void Success(string message);
    void Warn(string message);
    void Error(string message);
    void Error(Exception ex);
}

public interface INotificationStream
{
    IEventSubscriber<string> Informed { get; }
    IEventSubscriber<string> Succeeded { get; }
    IEventSubscriber<string> Warned { get; }
    IEventSubscriber<string> Failed { get; }
}
