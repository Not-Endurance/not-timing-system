using Not.Events;
using Not.Injection;

namespace Not.Notify;

public class Notifier : INotifier, INotificationStream, IScoped
{
    readonly Event<string> _informed = new();
    readonly Event<string> _succeeded = new();
    readonly Event<string> _warned = new();
    readonly Event<string> _failed = new();
    readonly Event<Exception> _unhandledExceptions = new();

    public IEventSubscriber<string> Informed => _informed;
    public IEventSubscriber<string> Succeeded => _succeeded;
    public IEventSubscriber<string> Warned => _warned;
    public IEventSubscriber<string> Failed => _failed;
    public IEventSubscriber<Exception> UnhandledExceptions => _unhandledExceptions;

    public void Inform(string message)
    {
        _informed.Emit(message);
    }

    public void Success(string message)
    {
        _succeeded.Emit(message);
    }

    public void Warn(string message)
    {
        _warned.Emit(message);
    }

    public void Error(string message)
    {
        _failed.Emit(message);
    }

    public void Error(Exception exception)
    {
        _unhandledExceptions.Emit(exception);
    }
}
