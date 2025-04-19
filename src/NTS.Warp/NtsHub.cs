using Microsoft.AspNetCore.SignalR;

namespace NTS.Warp;

public class NtsHub<T> : Hub<T>
    where T : class
{
    public override async Task OnConnectedAsync()
    {
        var query = Context.GetHttpContext()!.Request.Query;
        if (!query.TryGetValue(WarpConstants.EVENT_GROUP_ID_KEY, out var value))
        {
            Context.Abort();
        }
        var enduranceEventId = value.ToString();
        await Groups.AddToGroupAsync(Context.ConnectionId, enduranceEventId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, WarpConstants.EVENT_GROUP_ID_KEY);
    }
}
