using Not.Blazor.Ports.Behinds;
using NTS.Domain.Core.Entities;
using NTS.Domain.Core.Entities.ParticipationAggregate;
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
    Task RequestRepresent(bool requestFlag);
    Task RequireInspection(bool requestFlag);
    Task Process(Snapshot snapshot);
    Task Withdraw();
    Task Retire();
    Task FinishNotRanked(string reason);
    Task Disqualify(string reason);
    Task FailToQualify(FTQCodes[] ftqCodes, string? reason);
    Task RestoreQualification();
    Participation? Get(int id);
}
