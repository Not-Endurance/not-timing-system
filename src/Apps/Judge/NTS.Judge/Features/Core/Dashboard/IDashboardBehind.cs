using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Features.Core.Dashboard;

public interface IDashboardBehind : IParticipationContext
{
    // TODO: this should probably be removed and Participations can be returned from Start instead
    IEnumerable<Participation> Participations { get; }
    IReadOnlyList<int> RecentlyProcessed { get; }
}
