using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Not.Injection;

namespace NTS.Nexus.Warp.Features;

public class JudgeConnectionsContext : IJudgeConnectionsContext
{
    readonly ILogger<JudgeConnectionsContext> _logger;
    readonly ConcurrentDictionary<string, string> _connections = [];

    public JudgeConnectionsContext(ILogger<JudgeConnectionsContext> logger)
    {
        _logger = logger;
    }

    public void Add(string enduranceEventId, string connectionId)
    {
        if (_connections.TryAdd(enduranceEventId, connectionId))
        {
            return;
        }
        _logger.LogError("Connection with identifier '{enduranceEventId}' already exists", enduranceEventId);
        throw new HubException(
            $"Event '{enduranceEventId}' is already active and managed. Select a different event to proceed"
        ); // TODO: localize this
    }

    public void Remove(string connectionId)
    {
        var match = _connections.FirstOrDefault(x => x.Value == connectionId);
        _connections.TryRemove(match);
    }

    public string? GetConnectionId(string enduranceEventId)
    {
        return _connections.GetValueOrDefault(enduranceEventId);
    }
}

public interface IJudgeConnectionsContext : ISingleton
{
    string? GetConnectionId(string enduranceEventId);
}
