using NC.Exceptions;

namespace NC.SmartSearch;

public class InvalidSearchTermException : NotException
{
    public InvalidSearchTermException(string message)
        : base(message) { }
}
