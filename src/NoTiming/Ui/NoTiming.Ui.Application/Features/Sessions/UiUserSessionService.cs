using NC.Application.Authentication.Abstractions;
using NC.Injection;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Application.UserSession;
using NoTiming.Domain.Watcher;

namespace NoTiming.Ui.Application.Features.Sessions;

public class UiUserSessionService : IUiUserSession, IScoped
{
    readonly INUserSession _nUserSessionService;
    readonly INoTimingUserSessionRepository _userSessions;
    int? _eventId;

    public UiUserSessionService(INUserSession nUserSessionService, INoTimingUserSessionRepository userSessions)
    {
        _nUserSessionService = nUserSessionService;
        _userSessions = userSessions;
    }

    public async Task<NoTimingUserSessionStateModel?> GetCurrent()
    {
        if (_eventId == null)
        {
            return null;
        }

        var userSession = await _nUserSessionService.GetCurrent<NoTimingUserSessionStateModel>();
        if (userSession == null)
        {
            return null;
        }

        return (await _userSessions.ReadByUserIdentifier(userSession.UserIdentifier, _eventId.Value))?.State?.Copy();
    }

    public async Task SetEventId(int? eventId)
    {
        if (eventId == null)
        {
            return;
        }

        _eventId = eventId;

        var userSession = await _nUserSessionService.GetCurrent<NoTimingUserSessionStateModel>();
        if (userSession == null)
        {
            return;
        }

        var currentSession = await _userSessions.ReadByUserIdentifier(userSession.UserIdentifier, eventId.Value);
        if (currentSession != null)
        {
            return;
        }

        var session = CreateSession(userSession, eventId.Value, new NoTimingUserSessionStateModel());
        await _userSessions.Create(session);
    }

    public async Task AppendSnapshot(SnapshotGroup snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var sentNumbers = snapshot.Entries.Select(entry => entry.Number).ToHashSet();

        var userSession = await _nUserSessionService.GetCurrent<NoTimingUserSessionStateModel>();
        if (userSession == null || _eventId == null)
        {
            return;
        }

        var currentSession = await _userSessions.ReadByUserIdentifier(userSession.UserIdentifier, _eventId.Value);
        if (currentSession == null)
        {
            currentSession = CreateSession(
                userSession,
                _eventId.Value,
                new NoTimingUserSessionStateModel { SnapshotHistory = [SnapshotGroupModel.MapFrom(snapshot)] }
            );
            await _userSessions.Create(currentSession);
            return;
        }

        var currentState = currentSession.State?.Copy() ?? new NoTimingUserSessionStateModel();
        currentState.SnapshotHistory = [.. currentState.SnapshotHistory, SnapshotGroupModel.MapFrom(snapshot)];
        currentState.SnapshotSelections = currentState
            .SnapshotSelections.Where(selection => !sentNumbers.Contains(selection.Number))
            .ToArray();
        currentSession.ReplaceState(currentState);
        await _userSessions.Update(currentSession);
    }

    public async Task ReplaceSnapshotSelections(IReadOnlyCollection<Snapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        var userSession = await _nUserSessionService.GetCurrent<NoTimingUserSessionStateModel>();
        if (userSession == null || _eventId == null)
        {
            return;
        }

        var currentSession = await _userSessions.ReadByUserIdentifier(userSession.UserIdentifier, _eventId.Value);
        var currentState = currentSession?.State?.Copy() ?? new NoTimingUserSessionStateModel();
        currentState.SnapshotSelections = snapshots.Select(SnapshotModel.MapFrom).ToArray();

        if (currentSession == null)
        {
            currentSession = CreateSession(userSession, _eventId.Value, currentState);
            await _userSessions.Create(currentSession);
            return;
        }

        currentSession.ReplaceState(currentState);
        await _userSessions.Update(currentSession);
    }

    public async Task DeleteCurrent()
    {
        var userSession = await _nUserSessionService.GetCurrent<NoTimingUserSessionStateModel>();
        if (userSession == null || _eventId == null)
        {
            return;
        }

        var currentSession = await _userSessions.ReadByUserIdentifier(userSession.UserIdentifier, _eventId.Value);
        if (currentSession == null)
        {
            return;
        }

        await _userSessions.Delete(currentSession);
    }

    static NoTimingUserSessionModel CreateSession(
        INUserSessionModel userSession,
        int eventId,
        NoTimingUserSessionStateModel? state
    )
    {
        var session = new NoTimingUserSessionModel
        {
            Id = userSession.User.Id,
            EventId = eventId,
            UserIdentifier = userSession.UserIdentifier,
        };
        session.ReplaceState(state);
        return session;
    }
}
