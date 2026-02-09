namespace NTS.Warp.Features.Judge.Procedures;

public interface IStatusClientProcedures
{
    Task OnWitnessConnected(string connectionId);
    Task OnWitnessDisconnected(string connectionId);
}
