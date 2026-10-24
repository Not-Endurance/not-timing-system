using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Aggregates;

namespace NTS.Contracts.Core;

public interface IParticipationContext : IStatefulService
{
    Participation? Selected { get; set; }
    IReadOnlyList<Participation> Participations { get; }
    IReadOnlyList<int> RecentlyTimed { get; }
}
