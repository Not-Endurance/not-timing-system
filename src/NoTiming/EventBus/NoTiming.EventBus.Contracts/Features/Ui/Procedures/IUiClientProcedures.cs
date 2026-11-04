using NoTiming.Domain.Core.Objects.Payloads;

namespace NoTiming.EventBus.Contracts.Features.Ui.Procedures;

public interface IUiClientProcedures
{
    Task OnParticipationArrived(ParticipationArrived payload);
    Task OnInspectionRequired(InspectionRequired payload);
    Task OnRepresentationRequired(RepresentationRequired payload);
    Task OnPhaseCompleted(PhaseCompleted payload);
    Task OnParticipationEliminated(ParticipationEliminated payload);
    Task OnParticipationRestored(ParticipationRestored payload);
    Task OnPresentationAcknoledged(VetInAcknoledged payload);
}
