namespace Not.Exceptions;

/// <summary>
/// Use as base for domain exceptions (validation). That way functionalities like <seealso cref="Not.Notify.INotifier"/>
/// can work with Domain exceptions which are higher up on the dependency chain and cannot be referenced directly
/// </summary>
public abstract class ValidationException : ApplicationException
{
    protected ValidationException(string message)
        : base(message) { }

    protected ValidationException(string property, string message)
        : this(message)
    {
        Property = property;
    }

    public string? Property { get; }
}
