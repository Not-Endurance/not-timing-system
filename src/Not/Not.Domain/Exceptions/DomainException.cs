using Not.Exceptions;

namespace Not.Domain.Exceptions;

//TODO: Create AggregateDomainException and modify domains to batch their validation exceptions before throwing
public class DomainException : ValidationException
{
    public DomainException(string message)
        : base(message) { }
    
    public DomainException(string template, params object[] args)
        : base(string.Format(template, args)) { }
}

public class DomainPropertyException : DomainException
{
    public DomainPropertyException(string property, string message) : base(property, message)
    {
    }

    public DomainPropertyException(string property, string template, params object?[] args)
        : base(property, string.Format(template, args))
    {
    }
}
