using System.Net;
using Not.Application.HTTP;
using Not.Injection;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Features.Account;
using NTS.Contracts.Sessions.Models;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Objects;

namespace NoTiming.Ui.Features.Sessions;

/// <summary>
/// What a person keeps per Event (#645, #602): the Snapshots they have selected and the groups they have sent. The host
/// keeps it under their account, at <c>/api/user-sessions</c>, and gives it back in any tab and on any device, so the Ui
/// names no owner and holds nothing of it. A visitor keeps nothing, and nothing is kept before an Event is named. A host
/// that fails is not a person who has kept nothing: the read or the change fails, as the person's state is not to be taken
/// for empty and put over. A session that has ended is a visitor again, and the account is told so.
/// </summary>
public class WitnessUserSessionService : IWitnessUserSession, IScoped
{
    readonly IAccountSession _account;
    readonly UserSessionApiRepository _sessions;
    Guid? _eventId;

    public WitnessUserSessionService(IAccountSession account, UserSessionApiRepository sessions)
    {
        _account = account;
        _sessions = sessions;
    }

    public async Task<NtsUserSessionStateModel?> GetCurrent()
    {
        if (_eventId is not { } eventId || !await IsSignedIn())
        {
            return null;
        }

        return (await Find(eventId))?.State;
    }

    /// <summary>
    /// Names the Event whose state is read and changed. The host is not asked: connecting to an Event is for every viewer,
    /// and does not wait for, or fail with, what a person keeps. The record is made by the first change.
    /// </summary>
    public Task SetEventId(Guid? eventId)
    {
        _eventId = eventId ?? _eventId;
        return Task.CompletedTask;
    }

    public async Task AppendSnapshot(SnapshotGroup snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var sentNumbers = snapshot.Entries.Select(entry => entry.Number).ToHashSet();

        await Change(state =>
        {
            state.SnapshotHistory = [.. state.SnapshotHistory, SnapshotGroupModel.MapFrom(snapshot)];
            state.SnapshotSelections =
            [
                .. state.SnapshotSelections.Where(selection => !sentNumbers.Contains(selection.Number)),
            ];
        });
    }

    public async Task UpdateSnapshotInHistory(Guid groupId, int number, Timestamp time)
    {
        ArgumentNullException.ThrowIfNull(time);

        await Change(state =>
        {
            foreach (var entry in state.SnapshotHistory.Where(x => x.GroupId == groupId).SelectMany(x => x.Entries))
            {
                if (entry.Number == number)
                {
                    entry.Timestamp = time.ToString();
                    entry.Time = time.ToDateTimeOffset();
                }
            }
        });
    }

    public async Task ReplaceSnapshotSelections(IReadOnlyCollection<Snapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        await Change(state => state.SnapshotSelections = [.. snapshots.Select(SnapshotModel.MapFrom)]);
    }

    public async Task DeleteCurrent()
    {
        if (_eventId is not { } eventId || !await IsSignedIn())
        {
            return;
        }

        if (await Find(eventId) is { } kept)
        {
            await AnsweredUnit(() => _sessions.Delete(kept.Id));
        }
    }

    /// <summary>
    /// Changes the state the host keeps of the person for the Event. It is read as it is now, as another tab may have
    /// changed it, the change is made to it, and it is put back whole; the record is made when there is none, and making
    /// it and reading it are one request, so that the record of another tab is never replaced by a new one.
    /// </summary>
    async Task Change(Action<NtsUserSessionStateModel> change)
    {
        if (_eventId is not { } eventId || !await IsSignedIn())
        {
            return;
        }

        UserSessionModel kept;
        try
        {
            kept = await _sessions.Make(eventId);
        }
        catch (JsonApiException refused) when (IsSessionEnded(refused))
        {
            await _account.Refresh();
            return;
        }

        change(kept.State);
        await AnsweredUnit(() => _sessions.Save(kept));
    }

    async Task<UserSessionModel?> Find(Guid eventId)
    {
        return await Answered(() => _sessions.FindOf(eventId));
    }

    /// <summary>
    /// Whether somebody is signed in. A host that has not said is not a visitor: nothing is read or kept on a guess, as
    /// what the person has is not to be shown as empty, and a record made on one would be put over theirs.
    /// </summary>
    async Task<bool> IsSignedIn()
    {
        await _account.Load();
        return _account.IsKnown
            ? _account.IsSignedIn
            : throw new InvalidOperationException("The host has not said who is signed in.");
    }

    /// <summary>The answer of the host, none when the session has ended: the account is asked again, and a visitor keeps nothing.</summary>
    async Task<T?> Answered<T>(Func<Task<T?>> request)
        where T : class
    {
        try
        {
            return await request();
        }
        catch (JsonApiException refused) when (IsSessionEnded(refused))
        {
            await _account.Refresh();
            return null;
        }
    }

    async Task AnsweredUnit(Func<Task> request)
    {
        try
        {
            await request();
        }
        catch (JsonApiException refused) when (IsSessionEnded(refused))
        {
            await _account.Refresh();
        }
    }

    static bool IsSessionEnded(JsonApiException refused)
    {
        return refused.Response.Status == HttpStatusCode.Unauthorized;
    }
}
