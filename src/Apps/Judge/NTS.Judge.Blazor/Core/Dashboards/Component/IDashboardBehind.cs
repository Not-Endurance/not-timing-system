using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Blazor.Core.Dashboards.Component;

public interface IDashboardBehind : IParticipationContext
{
    // TODO: this should probably be removed and Participations can be returned from Start instead
    IEnumerable<Participation> Participations { get; }
    IReadOnlyList<int> RecentlyProcessed { get; }
}
