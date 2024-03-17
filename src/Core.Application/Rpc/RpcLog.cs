using System;

namespace Core.Application.Rpc;

public struct RpcLog
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

    public string ClientId { get; private set; }
    public string Message { get; private set; }
    public DateTimeOffset DateTime { get; private set; }
}
