using NTS.Domain.Core.Objects.Payloads;

namespace NTS.Warp.Features.Witness.Procedures;

public interface IWitnessClientProcedures
{
    Task OnPhaseCompleted(PhaseCompleted payload);
    Task OnParticipationEliminated(ParticipationEliminated payload);
    Task OnParticipationRestored(ParticipationRestored payload);
}
