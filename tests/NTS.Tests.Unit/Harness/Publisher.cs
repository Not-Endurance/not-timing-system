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
using NTS.Domain.Core.Aggregates.Participations.Events;

namespace NTS.Tests.Unit.Harness;

internal sealed class Publisher : ISnapshotPublisher
{
    TaskCompletionSource? _held;

    public List<SnapshotGroup> Published { get; } = [];
    public Action<SnapshotGroup>? Before { get; set; }
    public Exception? Fail { get; set; }

    public Func<int, Guid, SnapshotReceipt> Answer { get; set; } =
        (_, id) => SnapshotReceipt.Recorded(id, 201, new RecordedSnapshot { Outcome = TimeEventOutcome.Accepted });

    public void Hold()
    {
        _held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Release()
    {
        _held?.SetResult();
    }

    public async Task<IReadOnlyList<SnapshotReceipt>> PublishSnapshotsAsync(
        Guid eventId,
        SnapshotGroup snapshotGroup,
        CancellationToken cancellationToken = default
    )
    {
        Published.Add(snapshotGroup);
        Before?.Invoke(snapshotGroup);
        if (_held != null)
        {
            await _held.Task;
        }

        if (Fail != null)
        {
            throw Fail;
        }

        return [.. snapshotGroup.Entries.Select(x => Answer(x.Number, snapshotGroup.IdOf(x)))];
    }

    public Task<SnapshotReceipt> UpdateSnapshotAsync(
        Guid snapshotId,
        DateTimeOffset time,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(Answer(0, snapshotId));
    }
}
