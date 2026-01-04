namespace Not.Application.RPC.Clients;

public class RpcInvokeResult //TODO: RpcInvokeResult seems kind of pointless. Consider removing
{
    protected RpcInvokeResult() { }

    public bool IsSuccessful { get; protected set; }

    public static RpcInvokeResult Success => new() { IsSuccessful = true };

    public static RpcInvokeResult Error => new();
}

public class RpcInvokeResult<T> : RpcInvokeResult
{
    public static new RpcInvokeResult<T> Success(T data)
    {
        return new() { Data = data, IsSuccessful = true };
    }

    RpcInvokeResult() { }

    public T? Data { get; private set; }

    public static new RpcInvokeResult<T> Error => new() { };
}
