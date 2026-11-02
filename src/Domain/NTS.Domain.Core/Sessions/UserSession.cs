using System.Collections.Generic;
using System.Linq;
using Not.Domain.Abstractions;
using NTS.Domain.Core.Objects.Snapshots;

namespace NTS.Domain.Core.Sessions;

/// <summary>
/// What a person keeps for one Event: the groups of Snapshots they have sent and the Snapshots they have selected (ADR-0013,
/// #602). It is the record of one account, and the Api decides who may read or change it; the entity holds what the record
/// says and nothing of who keeps it.
/// </summary>
public sealed class UserSession : IEntity
{
    public UserSession(
        Guid id,
        Guid eventId,
        IEnumerable<SnapshotGroup>? history = null,
        IEnumerable<Snapshot>? selections = null
    )
    {
        Id = id == Guid.Empty ? throw new ArgumentException("A session has an id.", nameof(id)) : id;
        EventId = eventId == Guid.Empty ? throw new ArgumentException("A session is for an Event.", nameof(eventId)) : eventId;
        SnapshotHistory = (history ?? []).ToArray();
        SnapshotSelections = (selections ?? []).ToArray();
    }

    public Guid Id { get; }
    public Guid EventId { get; }
    public IReadOnlyList<SnapshotGroup> SnapshotHistory { get; }
    public IReadOnlyList<Snapshot> SnapshotSelections { get; }
}
