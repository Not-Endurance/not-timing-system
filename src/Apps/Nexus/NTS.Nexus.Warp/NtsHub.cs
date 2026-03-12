using Microsoft.AspNetCore.SignalR;
using Not.Application.RPC;

namespace NTS.Nexus.Warp;

public class NtsHub<T> : Hub<T>
    where T : class
{
    readonly ILogger<NtsHub<T>> _logger;

    public NtsHub(ILogger<NtsHub<T>> logger)
    {
        _logger = logger;
    }

    protected string? GetConnectionGroup()
    {
        return Context.GetHttpContext()!.Request.Query[RpcConstants.CONNECTION_GROUP_KEY].ToString();
    }

    public override async Task OnConnectedAsync()
    {
        var query = Context.GetHttpContext()!.Request.Query;
        if (!query.TryGetValue(RpcConstants.CONNECTION_GROUP_KEY, out var value))
        {
            const string message = "SignalR connection rejected because the event ID query parameter is missing.";
            _logger.LogInformation("******* {Message} *******", message);
            throw new InvalidOperationException(message);
        }
        var enduranceEventId = value.ToString();
        await Groups.AddToGroupAsync(Context.ConnectionId, enduranceEventId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RpcConstants.CONNECTION_GROUP_KEY);
    }
}
