namespace Not.Application.RPC;

public class RpcSettings
{
    public string Host { get; init; } = default!;
    public string HubPattern { get; init; } = default!;
    public int? Port { get; init; }

    public string Url => Port == null ? $"{Host}/{HubPattern}" : $"{Host}:{Port}/{HubPattern}";
}
