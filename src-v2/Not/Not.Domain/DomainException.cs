using Not.Exceptions;

namespace Not.Domain;

//TODO: Create AggregateDomainException and modify domains to batch their validation exceptions before throwing 
public class DomainException : DomainExceptionBase
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string property, string message) : base(property, message)
    {
    }
}
