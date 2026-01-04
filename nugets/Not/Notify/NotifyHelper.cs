using Not.Exceptions;
using Not.Strings;

namespace Not.Notify;

public static class NotifyHelper
{
    public static void Inform(string message)
    {
        NotificationEvents.INFORMED.Emit(message);
    }

    public static void Success(string message)
    {
        NotificationEvents.SUCCEDED.Emit(message);
    }

    public static void Warn(string message)
    {
        NotificationEvents.WARNED.Emit(message);
    }

    public static void Warn(ValidationException validation)
    {
        Warn(validation.Message);
    }

    public static void Error(string message)
    {
        NotificationEvents.FAILED.Emit(message);
    }

    public static void Error(Exception exception)
    {
        exception = exception.GetBaseException();
        var message = exception.Message + Environment.NewLine + exception.StackTrace?.NTrim(1000);
        Error(message);
    }
}
