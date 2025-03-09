namespace Not.Exceptions;

public static class ExceptionExtensions
{
    public static Exception GetInnermost(this Exception exception)
    {
        if (exception.InnerException == null)
        {
            return exception;
        }
        return GetInnermost(exception.InnerException);
    }
}
