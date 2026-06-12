using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.Contracts.Features.Performance;

public interface IPerformanceParticipations : IStatefulService
{
    IReadOnlyList<Participation> Participations { get; }
}
