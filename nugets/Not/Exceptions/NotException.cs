namespace Not.Exceptions;

public class NotException : ApplicationException
{
    public NotException(string message)
        : base(message) { }
}
