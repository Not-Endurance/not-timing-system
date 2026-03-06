using System;
using Not.Events;
using Not.Injection;
using Not.Strings;

namespace Not.Notify;

public class Notifier : INotifier, INotificationStream, ISingleton
{
    readonly Event<string> _informed = new();
    readonly Event<string> _succeeded = new();
    readonly Event<string> _warned = new();
    readonly Event<string> _failed = new();

    public IEventSubscriber<string> Informed => _informed;
    public IEventSubscriber<string> Succeeded => _succeeded;
    public IEventSubscriber<string> Warned => _warned;
    public IEventSubscriber<string> Failed => _failed;

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
        exception = exception.GetBaseException();
        var message = exception.Message + Environment.NewLine + exception.StackTrace?.NTrim(1000);
        Error(message);
    }
}
