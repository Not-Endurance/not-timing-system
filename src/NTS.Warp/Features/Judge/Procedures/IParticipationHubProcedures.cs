using NTS.Domain.Core.Objects.Payloads;

namespace NTS.Warp.Features.Judge.Procedures;

public interface IParticipationHubProcedures
{
    Task OnPhaseCompleted(WarpRequest<PhaseCompleted> request);
    Task OnParticipationEliminated(WarpRequest<ParticipationEliminated> request);
    Task OnParticipationRestored(WarpRequest<ParticipationRestored> request);
}
