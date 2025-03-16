namespace NTS.ACL.Abstractions;

/// <summary>
/// Shorthand for DomainExceptionBase
/// </summary>
public static class EmsHelper
{
    internal static T Create<T>(string message)
        where T : EmsDomainExceptionBase, new()
    {
        return EmsDomainExceptionBase.Create<T>(message);
    }

    internal static T Create<T>(string message, params object[] arguments)
        where T : EmsDomainExceptionBase, new()
    {
        return EmsDomainExceptionBase.Create<T>(message, arguments);
    }

    internal static EmsDomainException Create(string entity, string message)
    {
        var exception = new EmsDomainException(entity, message);
        return exception;
    }

    internal static EmsDomainException Create(string entity, string message, params object[] arguments)
    {
        var exception = new EmsDomainException(entity, message, arguments);
        return exception;
    }
}
