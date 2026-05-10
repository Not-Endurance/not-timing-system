using Not.Application.Authentication.Abstractions;
using Not.Injection;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Application.UserSession;
using NTS.Domain.Watcher;

namespace NTS.Witness.Features.Sessions;

public class WitnessUserSessionService : IWitnessUserSession, IScoped
{
    readonly INUserSession _nUserSessionService;
    readonly INtsUserSessionRepository _userSessions;
    int? _eventId;

    public WitnessUserSessionService(INUserSession nUserSessionService, INtsUserSessionRepository userSessions)
    {
        _nUserSessionService = nUserSessionService;
        _userSessions = userSessions;
    }

    public async Task<NtsUserSessionStateModel?> GetCurrent()
    {
        if (_eventId == null)
        {
            return null;
        }

        var userSession = await _nUserSessionService.GetCurrent<NtsUserSessionStateModel>();
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

        var userSession = await _nUserSessionService.GetCurrent<NtsUserSessionStateModel>();
        if (userSession == null)
        {
            return;
        }

        var currentSession = await _userSessions.ReadByUserIdentifier(userSession.UserIdentifier, eventId.Value);
        if (currentSession != null)
        {
            return;
        }

        var session = CreateSession(userSession, eventId.Value, new NtsUserSessionStateModel());
        await _userSessions.Create(session);
    }

    public async Task AppendSnapshot(SnapshotGroup snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var userSession = await _nUserSessionService.GetCurrent<NtsUserSessionStateModel>();
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
                new NtsUserSessionStateModel { SnapshotHistory = [SnapshotGroupModel.MapFrom(snapshot)] }
            );
            await _userSessions.Create(currentSession);
            return;
        }

        var currentState = currentSession.State?.Copy() ?? new NtsUserSessionStateModel();
        currentState.SnapshotHistory = [.. currentState.SnapshotHistory, SnapshotGroupModel.MapFrom(snapshot)];
        currentSession.ReplaceState(currentState);
        await _userSessions.Update(currentSession);
    }

    public async Task DeleteCurrent()
    {
        var userSession = await _nUserSessionService.GetCurrent<NtsUserSessionStateModel>();
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

    static NtsUserSessionModel CreateSession(
        INUserSessionModel userSession,
        int eventId,
        NtsUserSessionStateModel? state
    )
    {
        var session = new NtsUserSessionModel
        {
            Id = userSession.User.Id,
            EventId = eventId,
            UserIdentifier = userSession.UserIdentifier,
        };
        session.ReplaceState(state);
        return session;
    }
}
