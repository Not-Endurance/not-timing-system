using Not.Events;

namespace Not.Notify;

public static class NotificationEvents
{
    public static readonly Event<string> INFORMED = new();
    public static readonly Event<string> SUCCEDED = new();
    public static readonly Event<string> WARNED = new();
    public static readonly Event<string> FAILED = new();
}
