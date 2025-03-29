namespace Not.Exceptions;

/// <summary>
/// Use as base for domain exceptions (validation). That way functionalities like <seealso cref="Notifier.NotifyHelper"/>
/// can work with Domain exceptions which are higher up on the dependency chain and cannot be referenced directly
/// </summary>
public abstract class ValidationException : ApplicationException
{
    protected ValidationException(string message)
        : base(message) { }

    protected ValidationException(string propertyName, string message)
        : this(message)
    {
        Property = propertyName;
    }

    public string? Property { get; }
}
