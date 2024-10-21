namespace Not.Exceptions;

/// <summary>
/// Use as base for domain exceptions (validation). That way functionalities like <seealso cref="Notifier.NotifyHelper"/>
/// can work with Domain exceptions which are higher up on the dependency chain and cannot be referenced directly
/// </summary>
public abstract class DomainExceptionBase : ApplicationException
{
    protected DomainExceptionBase(string message) : base(message)
    {
    }

    protected DomainExceptionBase(string property, string message) : this(message)
    {
        Property = property;
    }

    public string? Property { get; }
}
