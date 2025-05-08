using System.Collections.Concurrent;
using Not.Injection;

namespace NTS.Warp.Features;

public class PrimaryConnectionsContext : IPrimaryConnectionContext
{
    readonly ILogger<PrimaryConnectionsContext> _logger;
    readonly ConcurrentDictionary<string, string> _connections = [];

    public PrimaryConnectionsContext(ILogger<PrimaryConnectionsContext> logger)
    {
        _logger = logger;
    }

    public void Add(string identifier, string connectionId)
    {
        if (_connections.TryAdd(identifier, connectionId))
        {
            return;
        }
        _logger.LogError("Connection with identifier '{identifier}' already exists", identifier);
        throw new Exception("Duplicate connection attempt");
    }

    public void Remove(string connectionId)
    {
        var match = _connections.FirstOrDefault(x => x.Value == connectionId);
        _connections.TryRemove(match);
    }

    public string? GetConnectionId(string identifier)
    {
        return _connections.GetValueOrDefault(identifier);
    }
}

public interface IPrimaryConnectionContext : ISingleton
{
    string? GetConnectionId(string identifier);
}
