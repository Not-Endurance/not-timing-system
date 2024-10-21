using Not.Events;
using Not.Localization;

namespace Not.Notifier;

public abstract class NotifyEvent(string message)
{
    public string Message { get; } = message;
}

public class Informed(string message) : NotifyEvent(message.Localize())
{
}

public class Warned(string message) : NotifyEvent(message.Localize())
{
}

public class Succeeded(string message) : NotifyEvent(message.Localize())
{
}

public class Failed(string message) : NotifyEvent(message)
{
}
