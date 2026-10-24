using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Aggregates;

namespace NTS.Contracts.Core;

/// <summary>
/// The one owner of the Participations of the connected Event in an app (ADR-0006). It holds every Participation,
/// eliminated and completed ones included, and every list and page is a view over it: the views decide what to show.
/// </summary>
public interface IParticipationStore : IStatefulService
{
    IReadOnlyList<Participation> Participations { get; }
    Participation? Find(Guid id);
}
