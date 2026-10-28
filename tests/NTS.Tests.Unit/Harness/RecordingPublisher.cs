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

internal sealed class RecordingPublisher : ISnapshotPublisher
{
    public List<SnapshotGroup> Published { get; } = [];
    public List<Guid> Events { get; } = [];

    /// <summary>What the request fails with when a test says so: the group is taken by the server or not, and the answer is lost.</summary>
    public Exception? Fail { get; set; }

    /// <summary>What the server says of the Snapshot of a start number with the id it was sent under: recorded and accepted, unless a test says otherwise.</summary>
    public Func<int, Guid, SnapshotReceipt> Answer { get; set; } =
        (_, id) => SnapshotReceipt.Recorded(id, 201, SnapshotDoubles.Recorded(TimeEventOutcome.Accepted));

    /// <summary>The Updates the server was asked for: the id of the Snapshot and the instant it was to have.</summary>
    public List<(Guid Id, DateTimeOffset Time)> Updates { get; } = [];

    /// <summary>What the Update fails with when a test says so: the answer is lost.</summary>
    public Exception? UpdateFails { get; set; }

    /// <summary>What the server says of an Update, when a test says something other than what it says of a Snapshot.</summary>
    public Func<Guid, SnapshotReceipt>? UpdateAnswer { get; set; }

    public Task<IReadOnlyList<SnapshotReceipt>> PublishSnapshotsAsync(
        Guid eventId,
        SnapshotGroup snapshotGroup,
        CancellationToken cancellationToken = default
    )
    {
        Events.Add(eventId);
        Published.Add(snapshotGroup);
        if (Fail != null)
        {
            throw Fail;
        }

        IReadOnlyList<SnapshotReceipt> receipts =
        [
            .. snapshotGroup.Entries.Select(x => Answer(x.Number, snapshotGroup.IdOf(x))),
        ];
        return Task.FromResult(receipts);
    }

    public Task<SnapshotReceipt> UpdateSnapshotAsync(
        Guid snapshotId,
        DateTimeOffset time,
        CancellationToken cancellationToken = default
    )
    {
        Updates.Add((snapshotId, time));
        if (UpdateFails != null)
        {
            throw UpdateFails;
        }

        return Task.FromResult(UpdateAnswer?.Invoke(snapshotId) ?? Answer(0, snapshotId));
    }
}
