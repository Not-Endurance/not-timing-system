namespace NTS.Application.RPC;

public interface IConnectionsClientProcedures
{
    Task ReceiveRemoteConnectionId(string connectionId);
    Task ReceiveRemoteDisconnectId(string connectionId);
}
