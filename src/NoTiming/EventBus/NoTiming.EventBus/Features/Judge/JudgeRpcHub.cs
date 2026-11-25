using NoTiming.EventBus.Abstractions;
using NoTiming.EventBus.Contracts.Features.Judge.Procedures;

namespace NoTiming.EventBus.Features.Judge;

/// <summary>
/// Judge clients join this hub to receive change notices. They send nothing through it: writes go through the API.
/// Only one Judge may be connected to an event at a time.
/// </summary>
internal class JudgeRpcHub : NoTimingHubBase<IJudgeClientProcedures>
{
    readonly JudgeConnectionsContext _connections;

    public JudgeRpcHub(ILogger<JudgeRpcHub> logger, JudgeConnectionsContext connections)
        : base(logger)
    {
        _connections = connections;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        _connections.Add(GetConnectionGroup()!, Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        _connections.Remove(Context.ConnectionId);
    }
}
