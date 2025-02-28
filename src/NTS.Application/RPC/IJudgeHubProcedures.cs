using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Objects;

namespace NTS.Application.RPC;

public interface IJudgeHubProcedures
{
    Task SendStartCreated(PhaseCompleted phaseCompleted);
    Task SendParticipationEliminated(ParticipationEliminated revoked);
    Task SendParticipationRestored(ParticipationRestored restored);
}
