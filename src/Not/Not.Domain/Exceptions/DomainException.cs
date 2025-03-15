using Not.Exceptions;

namespace Not.Domain.Exceptions;

//TODO: Create AggregateDomainException and modify domains to batch their validation exceptions before throwing
public class DomainException : ValidationException
{
    public DomainException(string message)
        : base(message) { }

    public DomainException(string property, string message)
        : base(property, message) { }
}
