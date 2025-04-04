using Not.Exceptions;
using Not.Strings;

namespace Not.Domain.Exceptions;

//TODO: Create AggregateDomainException and modify domains to batch their validation exceptions before throwing
public class DomainException : ValidationException
{
    public DomainException(string message)
        : base(message) { }

    public DomainException(string template, params object?[] args)
        : base(template.Format(args)) { } // TODO: add template validation attribute wtf ever its name was
}

public class DomainPropertyException : ValidationException
{
    public DomainPropertyException(string property, string message)
        : base(property, message) { }

    public DomainPropertyException(string property, string template, params object?[] args)
        : base(property, template.Format(args)) { }
}
