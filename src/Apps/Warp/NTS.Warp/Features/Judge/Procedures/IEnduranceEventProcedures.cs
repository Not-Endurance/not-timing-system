namespace NTS.Warp.Features.Judge.Procedures;

public interface IEnduranceEventClientProcedures
{
    Task<int?> GetEventId();
}
