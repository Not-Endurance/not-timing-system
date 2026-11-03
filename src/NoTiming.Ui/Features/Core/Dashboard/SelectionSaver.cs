using Not.Async;
using NoTiming.Ui.Features.Sessions;
using NTS.Domain.Core.Objects.Snapshots;

namespace NoTiming.Ui.Features.Core.Dashboard;

/// <summary>
/// The person's selection is kept in their session as the page changes it. A save that is waiting takes the newest selection, and the
/// page waits for the last save before it writes history. A save that fails does not block the timing: the next change saves again,
/// since the server has the Snapshots themselves.
/// </summary>
public sealed class SelectionSaver
{
    const string KEY = "selections";

    readonly CoalesceInvoker<string> _saving = new();
    readonly IWitnessUserSession _session;
    Task _saved = Task.CompletedTask;

    public SelectionSaver(IWitnessUserSession session)
    {
        _session = session;
    }

    /// <summary>Completes when the latest save is made.</summary>
    public Task Saved => _saved;

    public void Save(IReadOnlyCollection<Snapshot> snapshots)
    {
        _saved = _saving.Invoke(KEY, () => WriteAsync(snapshots));
    }

    async Task WriteAsync(IReadOnlyCollection<Snapshot> snapshots)
    {
        try
        {
            await _session.ReplaceSnapshotSelections(snapshots);
        }
        catch
        {
            // Snapshot selection persistence must not block witness timing flow.
        }
    }
}
