namespace NTS.Warp.Features.Participations.Procedures;

public interface IEnduranceEventRpcClient
{
    Task<int?> GetEventId();
}
