using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Participations;

namespace NoTiming.Application.Contracts.Core;

/// <summary>
/// Commands a judge sends to the API. The API runs each rule and returns the participation's new state,
/// or null when the command was refused.
/// </summary>
public interface IParticipationCommands
{
    Task<Participation?> UpdatePhase(PhaseUpdateModel model);

    Task<Participation?> SetRepresentation(int participationId, bool isRequested);

    Task<Participation?> SetInspection(int participationId, bool isRequested);

    Task<Participation?> Withdraw(int participationId);

    Task<Participation?> Retire(int participationId);

    Task<Participation?> FinishNotRanked(int participationId, string reason);

    Task<Participation?> Disqualify(int participationId, DisqualifyCode[] codes, string? reason);

    Task<Participation?> FailToQualify(int participationId, FailToQualifyCode[] codes, string? reason);

    Task<Participation?> RestoreQualification(int participationId);

    /// <summary>Records a judge's snapshot and returns the participations it changed.</summary>
    Task<IReadOnlyList<Participation>> RecordSnapshots(SnapshotGroupModel group);
}
