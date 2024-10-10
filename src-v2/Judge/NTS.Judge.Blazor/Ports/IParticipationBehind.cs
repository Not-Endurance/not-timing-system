using Not.Blazor.Ports.Behinds;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;

namespace NTS.Judge.Blazor.Ports;

// TODO: break this monster in parts:
// - IParticipationSelector
// - IInspectionBehind
// - ISnapshotBehind
// - IEliminationBehind

public interface IParticipationBehind : IObservableBehind
{
    // TODO: this should probably be removed and Participations can be returned from Start instead
    IEnumerable<Participation> Participations { get; }
    Participation? SelectedParticipation { get; set; }
    Task RequestReinspection(bool requestFlag);
    Task RequestRequiredInspection(bool requestFlag);
    Task Process(Snapshot snapshot);
    Task Update(IPhaseState state);
    Task Withdraw();
    Task Retire();
    Task FinishNotRanked(string reason);
    Task Disqualify(string reason);
    Task FailToQualify(string? reason, FTQCodes[] ftqCodes);
    Task RestoreQualification();
    Participation? Get(int id);
}
