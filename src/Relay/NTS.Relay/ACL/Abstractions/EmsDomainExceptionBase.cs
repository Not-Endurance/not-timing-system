namespace NTS.Relay.ACL.Abstractions;

public abstract class EmsDomainExceptionBase : Exception
{
    protected abstract string Entity { get; }

    protected string InitMessage { get; init; }
    public override string Message => Prefix(InitMessage ?? Message);

    internal static T Create<T>(string message)
        where T : EmsDomainExceptionBase, new()
    {
        var exception = new T { InitMessage = message };
        return exception;
    }

    internal static T Create<T>(string message, params object[] arguments)
        where T : EmsDomainExceptionBase, new()
    {
        var exception = new T { InitMessage = string.Format(message, arguments) };
        return exception;
    }

    string Prefix(string message)
    {
        return $"{Entity} {message}";
    }
}
