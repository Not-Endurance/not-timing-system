using NTS.Warp.Features.Judge.Models;

namespace NTS.Warp.Features.Judge.Procedures;

public interface IJudgeHubProcedures
{
    Task SendStartCreated(ParticipationWarpDto participation);
    Task SendParticipationEliminated(ParticipationWarpDto participation);
    Task SendParticipationRestored(ParticipationWarpDto participation);
}
