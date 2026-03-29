namespace Not.Notify;

public static class NotificationHelper
{
    public static void Configure(INotifier? notifier)
    {
        _current = notifier;
    }

    public static void Clear(INotifier? notifier = null)
    {
        if (notifier == null || ReferenceEquals(_current, notifier))
        {
            _current = null;
        }
    }

    static INotifier? _current;
    public static INotifier? Current => _current;
}
