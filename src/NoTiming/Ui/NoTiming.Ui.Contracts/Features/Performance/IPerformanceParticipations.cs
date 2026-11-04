using NC.Application.Behinds.Adapters;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Ui.Contracts.Features.Performance;

public interface IPerformanceParticipations : IStatefulService
{
    IReadOnlyList<Participation> Participations { get; }
}
