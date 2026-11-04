namespace NC.Application.RPC;

public class RpcSettings
{
    public string Host { get; set; } = default!;

    // A path segment the client adds before the hub, for example "event-bus". Empty adds nothing.
    public string EndpointPrefix { get; init; } = string.Empty;

    public string HubPattern { get; init; } = default!;
    public int? Port { get; init; }
    public int ConnectTimeoutSeconds { get; init; } = 30;
    public string? ClientName { get; init; }
    public string? AppVersion { get; init; }

    public string Url
    {
        get
        {
            var host = Host.TrimEnd('/');
            var authority = Port == null ? host : $"{host}:{Port}";
            var segments = new[] { EndpointPrefix.Trim('/'), HubPattern.Trim('/') }.Where(segment =>
                segment.Length > 0
            );
            return $"{authority}/{string.Join('/', segments)}";
        }
    }
}
