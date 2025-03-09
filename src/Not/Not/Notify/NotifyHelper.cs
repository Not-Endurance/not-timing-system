using Not.Exceptions;
using Not.Strings;

namespace Not.Notify;

public static class NotifyHelper
{
    public static void Inform(string message)
    {
        NotificationEvents.Informed.Emit(new Information(message));
    }

    public static void Success(string message)
    {
        NotificationEvents.Succeded.Emit(new Success(message));
    }

    public static void Warn(string message)
    {
        NotificationEvents.Warned.Emit(new Warning(message));
    }

    public static void Warn(ValidationException validation)
    {
        Warn(validation.Message);
    }

    public static void Error(Exception exception)
    {
        exception = exception.GetInnermost();
        var failure = new Failure(exception.Message + Environment.NewLine + exception.StackTrace?.NTrim(1000));
        NotificationEvents.Failed.Emit(failure);
    }
}
