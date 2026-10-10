using Not.Events;
using Not.Notify;
using NoTiming.Ui.Features.Core.Dashboard;
using NTS.Contracts.Features.Account;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Application;

/// <summary>The group that was sent and not answered, as the browser keeps it: by person and Event, and as it was made.</summary>
internal sealed class InMemoryUnansweredSnapshots : IUnansweredSnapshots
{
    readonly Dictionary<(Guid, Guid), SnapshotGroup> _kept = [];

    /// <summary>A storage that cannot be written, as a browser can be: it keeps nothing and says nothing.</summary>
    public bool CannotKeep { get; set; }

    /// <summary>What was asked of the store, in order.</summary>
    public List<string> Calls { get; } = [];

    public SnapshotGroup? Of(Guid accountId, Guid eventId)
    {
        return _kept.GetValueOrDefault((accountId, eventId));
    }

    public Task<SnapshotGroup?> Read(Guid accountId, Guid eventId)
    {
        Calls.Add("read");
        return Task.FromResult(Of(accountId, eventId));
    }

    public Task Keep(Guid accountId, Guid eventId, SnapshotGroup group)
    {
        Calls.Add("keep");
        if (!CannotKeep)
        {
            _kept[(accountId, eventId)] = group;
        }

        return Task.CompletedTask;
    }

    public Task Forget(Guid accountId, Guid eventId)
    {
        Calls.Add("forget");
        _kept.Remove((accountId, eventId));
        return Task.CompletedTask;
    }
}

/// <summary>The time between two sends of a group that waits: it records how long the app waited and goes on when the test says.</summary>
internal sealed class StepTimer : ISnapshotResendTimer
{
    readonly List<TaskCompletionSource> _waiting = [];

    /// <summary>How long each wait was asked to be, in order.</summary>
    public List<TimeSpan> Spans { get; } = [];

    public bool IsWaiting
    {
        get
        {
            lock (_waiting)
            {
                return _waiting.Any(x => !x.Task.IsCompleted);
            }
        }
    }

    public Task Wait(TimeSpan span, CancellationToken cancellationToken)
    {
        var wait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => wait.TrySetCanceled(cancellationToken));
        lock (_waiting)
        {
            Spans.Add(span);
            _waiting.Add(wait);
        }

        return wait.Task;
    }

    /// <summary>Lets the wait that is on end, as the time passing does.</summary>
    public void Elapse()
    {
        lock (_waiting)
        {
            var wait =
                _waiting.LastOrDefault(x => !x.Task.IsCompleted)
                ?? throw new InvalidOperationException("Nothing waits for time to pass.");
            wait.TrySetResult();
        }
    }
}

/// <summary>Who is signed in, as the host said it, and how often the app was told to ask again.</summary>
internal sealed class FakeAccount : IAccountSession
{
    readonly Event _changed = new();

    public FakeAccount(Guid id)
    {
        Current = new CurrentAccount
        {
            Id = id,
            Email = "ana@example.test",
            ProfileComplete = true,
        };
    }

    public CurrentAccount? Current { get; set; }
    public bool IsSignedIn => Current != null;
    public bool IsKnown => true;
    public int Refreshes { get; private set; }
    public IEventSubscriber ObservableEvent => _changed;

    public Task Load()
    {
        return Task.CompletedTask;
    }

    public Task Refresh()
    {
        Refreshes++;
        return Task.CompletedTask;
    }

    public Task SignOut()
    {
        throw new NotSupportedException("The tests do not sign out.");
    }

    public Task SelectTenant(string? tenantId)
    {
        throw new NotSupportedException("The tests do not select a Tenant.");
    }
}

/// <summary>What the person was told, apart from what a page shows.</summary>
internal sealed class RecordingNotifier : INotifier
{
    public List<string> Warnings { get; } = [];

    public void Inform(string message) { }

    public void Success(string message) { }

    public void Warn(string message)
    {
        Warnings.Add(message);
    }

    public void Warn(IEnumerable<string> messages)
    {
        Warnings.AddRange(messages);
    }

    public void Error(string message)
    {
        Warnings.Add(message);
    }

    public void Error(Exception ex)
    {
        Warnings.Add(ex.Message);
    }
}

/// <summary>The ids a group sends its Snapshots under, which are what make a second send of it a repeat and not a record.</summary>
internal static class SnapshotIds
{
    public static Guid[] Of(SnapshotGroup group)
    {
        return [.. group.Entries.Select(group.IdOf)];
    }

    public static string[] Times(SnapshotGroup group)
    {
        return [.. group.Entries.Select(x => $"{x.Number}@{x.Timestamp!.ToDateTimeOffset():O}")];
    }
}
