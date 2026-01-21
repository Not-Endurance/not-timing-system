using Not.Application.RPC.SignalR;
using Not.Notify;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Witness.Services;
public class RpcInitializer : IRpcInitializer
{
    readonly IRpcSocket _rpcSocket;

    public RpcInitializer(IRpcSocket rpcSocket)
    {
        _rpcSocket = rpcSocket;
    }

    public UpcomingEvent? ConnectedEvent { get; set; } = default!;

    public bool IsConnected()
    {
        return _rpcSocket.IsConnected;
    }

    public async Task Disconnect()
    {
        if(ConnectedEvent != null)
        {
            await _rpcSocket.Disconnect();
            NotifyHelper.Warn("Disconnected from " + ConnectedEvent.Name);
            ConnectedEvent = null;
        }
    }

    public async Task StartConnection(UpcomingEvent enduranceEvent)
    {
        if (_rpcSocket.IsConnected)
        {
            NotifyHelper.Inform("Connected to " + ConnectedEvent?.Name);
            return;
        }
        await _rpcSocket.Connect();
        ConnectedEvent = enduranceEvent;
        NotifyHelper.Inform("Connected to " + ConnectedEvent.Name);
    }
}
