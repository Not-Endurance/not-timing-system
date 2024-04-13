using System;

namespace Core.Application.Rpc;

public class RpcContext
{
    private readonly RpcProtocls protocol;
    private readonly int port;
    private readonly string endpoint;
    private string? host;

    public RpcContext(RpcProtocls protocol, int port, string endpoint)
    {
        if (endpoint.StartsWith("/"))
        {
            endpoint = endpoint[1..];
        }
        this.protocol = protocol;
        this.port = port;
        this.endpoint = endpoint;
    }

    public string? Host
    {
        get => this.host; 
        internal set
        {
            if (value == null)
            {
                throw new ArgumentException("RPC host cannot be null", nameof(Host));
            }
            if (value.EndsWith("/") || value.EndsWith(":"))
            {
                this.host = value[..^1];
            }
            else
            {
                this.host = value;
            }
        }
    }
    public string? Url
        => this.Host == null
            ? null
            : $"{this.protocol.ToString().ToLower()}://{this.host}:{this.port}/{this.endpoint}";
}
