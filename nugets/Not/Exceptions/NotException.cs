namespace Not.Exceptions;

public class NotException : ApplicationException
{
    public NotException(string message)
        : base(message) { }

    public NotException(string message, Exception? inner)
        : base(message, inner) { }
}
