using NTS.Domain.Core.Objects.Payloads;

namespace NTS.Nexus.Warp.Contracts.Features.Witness.Procedures;

public interface IWitnessClientProcedures
{
    Task OnParticipationArrived(ParticipationArrived payload);
    Task OnInspectionRequired(InspectionRequired payload);
    Task OnRepresentationRequired(RepresentationRequired payload);
    Task OnPhaseCompleted(PhaseCompleted payload);
    Task OnParticipationEliminated(ParticipationEliminated payload);
    Task OnParticipationRestored(ParticipationRestored payload);
}
