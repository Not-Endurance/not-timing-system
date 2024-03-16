using System;

namespace Core.Application.Rpc;

public readonly struct RpcLog
{
	public RpcLog(string clientId, string message)
    {
        ClientId = clientId;
        Message = message;
        DateTime = DateTimeOffset.Now;
    }

    public RpcLog(string clientId, Exception exception) : this(clientId, exception.Message + Environment.NewLine + exception.StackTrace)
    {
    }

    public string ClientId { get; }
    public string Message { get; }
    public DateTimeOffset DateTime { get; }
}
