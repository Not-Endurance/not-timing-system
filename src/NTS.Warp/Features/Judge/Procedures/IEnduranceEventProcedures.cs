namespace NTS.Warp.Features.Judge.Procedures;

public interface IEnduranceEventRpcClient
{
    Task<int?> GetEventId();
}
