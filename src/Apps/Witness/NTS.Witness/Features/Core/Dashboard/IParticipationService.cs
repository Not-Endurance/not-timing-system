using Not.Collections;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Watcher;
using Not.Observables.Structures;

namespace NTS.Witness.Features.Core.Dashboard;

public interface IParticipationService : IParticipationContext
{
    ObservableList<SnapshotGroup> History { get; }
    Task AppendHistory(SnapshotGroup snapshotGroup, int? eventId = null);
    void Update(Participation participation, NCollectionAction action);
}
