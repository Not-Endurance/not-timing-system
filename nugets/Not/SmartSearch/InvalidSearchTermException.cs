using Not.Exceptions;

namespace Not.SmartSearch;

public class InvalidSearchTermException : NotException
{
    public InvalidSearchTermException(string message)
        : base(message) { }
}
