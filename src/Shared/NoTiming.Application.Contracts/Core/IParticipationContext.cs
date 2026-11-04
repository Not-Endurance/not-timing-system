using NC.Application.Behinds.Adapters;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Application.Contracts.Core;

public interface IParticipationContext : IStatefulService
{
    Participation? Selected { get; set; }
    IReadOnlyList<Participation> Participations { get; }
    IReadOnlyList<int> RecentlyTimed { get; }
}
