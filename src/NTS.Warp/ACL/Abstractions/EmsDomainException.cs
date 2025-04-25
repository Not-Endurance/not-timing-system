namespace NTS.Warp.ACL.Abstractions;

public class EmsDomainException : EmsDomainExceptionBase
{
    public EmsDomainException(string entity, string message, params object[] arguments)
        : this(entity, string.Format(message, arguments)) { }

    public EmsDomainException(string entity, string message)
    {
        Entity = entity;
        InitMessage = message;
    }

    protected override string Entity { get; }
}
