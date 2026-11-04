using NTS.Domain.Core.Objects.Payloads;

namespace NoTiming.EventBus.Contracts.Features.Judge.Procedures;

public interface IJudgeHubProcedures
{
    Task OnParticipationArrived(NoTimingRequest<ParticipationArrived> request);
    Task OnInspectionRequired(NoTimingRequest<InspectionRequired> request);
    Task OnRepresentationRequired(NoTimingRequest<RepresentationRequired> request);
    Task OnPhaseCompleted(NoTimingRequest<PhaseCompleted> request);
    Task OnParticipationEliminated(NoTimingRequest<ParticipationEliminated> request);
    Task OnParticipationRestored(NoTimingRequest<ParticipationRestored> request);
}
