using NTS.Domain.Core.Objects.Payloads;

namespace NTS.Warp.Features.Judge.Procedures;

public interface IParticipationHubProcedures
{
    Task OnPhaseCompleted(PhaseCompleted completed);
    Task OnParticipationEliminated(ParticipationEliminated eliminated);
    Task OnParticipationRestored(ParticipationRestored restored);
}
