using Not.Domain.Exceptions;
using NoTiming.Ui.Features.Core.Dashboard;
using NoTiming.Ui.Features.Core.EventViews;
using NoTiming.Ui.Features.Core.Participations;
using NoTiming.Ui.Features.Sessions;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Features.Snapshots;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using static NTS.Tests.Unit.Harness.ViewedEventFixtures;

namespace NTS.Tests.Unit.Harness;

internal sealed class NoSession : IWitnessUserSession
{
    /// <summary>The Snapshots the session kept as selected, if the test says it kept any.</summary>
    public IReadOnlyList<Snapshot>? Selections { get; set; }

    public Task<NtsUserSessionStateModel?> GetCurrent()
    {
        return Task.FromResult(
            Selections == null
                ? null
                : new NtsUserSessionStateModel
                {
                    SnapshotSelections = [.. Selections.Select(SnapshotModel.MapFrom)],
                }
        );
    }

    public Task SetEventId(Guid? eventId)
    {
        return Task.CompletedTask;
    }

    public Task AppendSnapshot(SnapshotGroup snapshot)
    {
        return Task.CompletedTask;
    }

    public Task UpdateSnapshotInHistory(Guid groupId, int number, Timestamp time)
    {
        return Task.CompletedTask;
    }

    public Task ReplaceSnapshotSelections(IReadOnlyCollection<Snapshot> snapshots)
    {
        return Task.CompletedTask;
    }

    public Task DeleteCurrent()
    {
        return Task.CompletedTask;
    }
}
