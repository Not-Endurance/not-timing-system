namespace NTS.Warp.Features.Judge.Procedures;

public interface IConnectionsClientProcedures
{
    Task OnWitnessConnected(string connectionId);
    Task OnWitnessDisconnected(string connectionId);
}
