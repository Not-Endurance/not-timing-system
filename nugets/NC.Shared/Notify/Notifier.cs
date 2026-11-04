using System.Text;
using Not.Events;
using Not.Injection;
using Not.Localization;

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

    public void Warn(IEnumerable<string> messages)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Validation_errors_string);
        foreach (var message in messages)
        {
            sb.AppendLine($" - {message}");
        }
        _warned.Emit(sb.ToString());
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
