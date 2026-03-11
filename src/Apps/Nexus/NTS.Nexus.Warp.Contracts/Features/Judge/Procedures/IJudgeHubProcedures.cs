using NTS.Domain.Core.Objects.Payloads;

namespace NTS.Nexus.Warp.Contracts.Features.Judge.Procedures;

public interface IJudgeHubProcedures
{
    Task OnPhaseCompleted(WarpRequest<PhaseCompleted> request);
    Task OnParticipationEliminated(WarpRequest<ParticipationEliminated> request);
    Task OnParticipationRestored(WarpRequest<ParticipationRestored> request);
}

