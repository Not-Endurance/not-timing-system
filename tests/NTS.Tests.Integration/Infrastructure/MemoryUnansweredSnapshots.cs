using System.Collections.Concurrent;
using NoTiming.Ui.Features.Core.Dashboard;
using NTS.Domain.Core.Objects.Snapshots;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// What the browser keeps of a group that was sent and not answered, held in memory for a Witness that is not in a browser.
/// A test that stands for a page that is lost and opened again gives the new Witness the same store.
/// </summary>
internal sealed class MemoryUnansweredSnapshots : IUnansweredSnapshots
{
    readonly ConcurrentDictionary<(Guid, Guid), SnapshotGroup> _kept = new();

    public SnapshotGroup? Of(Guid accountId, Guid eventId)
    {
        return _kept.GetValueOrDefault((accountId, eventId));
    }

    public Task<SnapshotGroup?> Read(Guid accountId, Guid eventId)
    {
        return Task.FromResult(Of(accountId, eventId));
    }

    public Task Keep(Guid accountId, Guid eventId, SnapshotGroup group)
    {
        _kept[(accountId, eventId)] = group;
        return Task.CompletedTask;
    }

    public Task Forget(Guid accountId, Guid eventId)
    {
        _kept.TryRemove((accountId, eventId), out _);
        return Task.CompletedTask;
    }
}
