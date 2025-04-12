namespace Not.Application.RPC.SignalR;

public class SignalRContext
{
    readonly RpcProtocol _protocol;
    readonly int? _port;
    readonly string _hubPattern;
    readonly string _host;

    public SignalRContext(RpcProtocol protocol, string host, string hubPattern, int?  port = null)
    {
        _protocol = protocol;
        _host = NormalizeHost(host);
        _hubPattern = NormalizePattern(hubPattern);
        _port = port;
    }

    public string Url => _port == null
        ? $"{_protocol.ToString().ToLower()}://{_host}/{_hubPattern}"
        : $"{_protocol.ToString().ToLower()}://{_host}:{_port}/{_hubPattern}";

    string NormalizePattern(string hubPattern)
    {
        if (hubPattern.StartsWith('/'))
        {
            hubPattern = hubPattern[1..];
        }
        return hubPattern;
    }

    string NormalizeHost(string host)
    {
        if (host.EndsWith('/') || host.EndsWith(':'))
        {
            host = host[..^1];
        }
        return host;
    }
}
