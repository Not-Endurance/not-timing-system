using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Entities;

namespace NTS.Persistence.States;

public class CoreState : NotState,
    ITreeState<Event>,
    ISetState<Official>,
    ISetState<Participation>,
    ISetState<Classification>,
    ISetState<SnapshotResult>
{
    public Event? Event { get; set; }
    public List<Official> Officials { get; } = new();
    public List<Participation> Participations { get; } = new();
    public List<Classification> Classifications { get; } = new();
    public List<SnapshotResult> SnapshotResults { get; } = new();

    Event? ITreeState<Event>.Root { get => Event; set => Event = value; }
    List<Official> ISetState<Official>.EntitySet => Officials;
    List<Participation> ISetState<Participation>.EntitySet => Participations;
    List<Classification> ISetState<Classification>.EntitySet => Classifications;
    List<SnapshotResult> ISetState<SnapshotResult>.EntitySet => SnapshotResults;
}
