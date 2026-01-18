using Not.Application.RPC.SignalR;
using Not.Notify;

namespace NTS.Witness.Services;
public class RpcInitializer : IRpcInitializer
{
    readonly IRpcSocket _rpcSocket;

    public RpcInitializer(IRpcSocket rpcSocket)
    {
        _rpcSocket = rpcSocket;
    }

    public async Task Disconnect()
    {
        await _rpcSocket.Disconnect();
        NotifyHelper.Warn("Disconnected from " + _rpcSocket.Connection);
    }

    public async Task StartConnection()
    {
        if (_rpcSocket.IsConnected)
        {
            return;
        }
        
        await _rpcSocket.Connect();
        NotifyHelper.Inform("Connected to " + _rpcSocket.Connection);
    }
}
