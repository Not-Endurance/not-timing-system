using Not.Domain.Exceptions;
using NoTiming.Ui.Features.Core.Dashboard;
using NoTiming.Ui.Features.Core.Participations;
using NoTiming.Ui.Features.Sessions;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Features.Snapshots;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using static NTS.Localization.NtsStrings;
using static NTS.Tests.Unit.Harness.ViewedEventFixtures;

namespace NTS.Tests.Unit.Harness;

internal sealed class SessionOf : IWitnessUserSession
{
    readonly NtsUserSessionStateModel? _session;

    public SessionOf(NtsUserSessionStateModel? session)
    {
        _session = session;
    }

    /// <summary>What keeping a sent group fails with when a test says so: the host failed or the session ended.</summary>
    public Exception? AppendFails { get; set; }

    /// <summary>What correcting a kept entry fails with when a test says so.</summary>
    public Exception? UpdateFails { get; set; }

    /// <summary>The corrections the service asked to be kept: the id of the group, the start number and the time.</summary>
    public List<(Guid GroupId, int Number, Timestamp Time)> Updated { get; } = [];

    public Task<NtsUserSessionStateModel?> GetCurrent()
    {
        return Task.FromResult(_session);
    }

    public Task SetEventId(Guid? eventId)
    {
        return Task.CompletedTask;
    }

    public Task AppendSnapshot(SnapshotGroup snapshot)
    {
        return AppendFails == null ? Task.CompletedTask : Task.FromException(AppendFails);
    }

    public Task UpdateSnapshotInHistory(Guid groupId, int number, Timestamp time)
    {
        Updated.Add((groupId, number, time));
        return UpdateFails == null ? Task.CompletedTask : Task.FromException(UpdateFails);
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
