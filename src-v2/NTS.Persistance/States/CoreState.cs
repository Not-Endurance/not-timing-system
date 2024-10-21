using NTS.Domain.Core.Entities;

namespace NTS.Persistence.States;

public class CoreState : NotState,
    ITreeState<EnduranceEvent>,
    ISetState<Official>,
    ISetState<Participation>,
    ISetState<Ranking>,
    ISetState<SnapshotResult>,
    ISetState<Handout>
{
    // The order here is very important due to how EntityReferenceEqualityGuardConverter works
    // Root level entities (Loop, Horse etc) MUST precede their parent entities (Combination, Phase)
    // in order to be the first to be serialized. Otherwise updates on those entities will be ignored
    // because the obsoleted entities will be serialized first and the updates will be lost to a $domainRef
    public EnduranceEvent? EnduranceEvent { get; set; }
    public List<Participation> Participations { get; } = [];
    public List<Official> Officials { get; } = [];
    public List<Ranking> Rankings { get; } = [];
    public List<SnapshotResult> SnapshotResults { get; } = [];
    public List<Handout> Handouts { get; } = [];

    EnduranceEvent? ITreeState<EnduranceEvent>.Root
    { 
        get => EnduranceEvent; 
        set => EnduranceEvent = value;
    }
    List<Official> ISetState<Official>.EntitySet => Officials;
    List<Participation> ISetState<Participation>.EntitySet => Participations;
    List<Ranking> ISetState<Ranking>.EntitySet => Rankings;
    List<SnapshotResult> ISetState<SnapshotResult>.EntitySet => SnapshotResults;
    List<Handout> ISetState<Handout>.EntitySet => Handouts;
}
