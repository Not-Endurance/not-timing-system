namespace NTS.Application.RPC;

public interface IConnectionsClientProcedures
{
    Task OnWitnessConnected(string connectionId);
    Task OnWitnessDisconnedted(string connectionId);
}
