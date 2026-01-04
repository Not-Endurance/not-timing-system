namespace Not.Application.RPC;

public class RpcError
{
    public RpcError(Exception exception, string? procedure, params object?[] arguments)
    {
        Exception = exception;
        Procedure = procedure;
        Arguments = arguments;
    }

    public Exception Exception { get; }
    public string? Procedure { get; }
    public object?[] Arguments { get; } = [];
}
