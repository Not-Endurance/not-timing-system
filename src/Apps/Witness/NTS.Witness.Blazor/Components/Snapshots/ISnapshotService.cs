using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.Blazor.Components.Snapshots;

public interface ISnapshotService
{
    List<Participation> GetParticipations();
}
