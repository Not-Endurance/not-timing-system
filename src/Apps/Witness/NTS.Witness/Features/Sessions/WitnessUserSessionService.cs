using Not.Application.Authentication.Abstractions;
using Not.Injection;
using NTS.Application.UserSession;
using NTS.Application.Watcher;
using NTS.Domain.Watcher;

namespace NTS.Witness.Features.Sessions;

public class WitnessUserSessionService : IWitnessUserSession, IScoped
{
    readonly INUserSession _nUserSessionService;
    readonly INtsUserSessionRepository _userSessions;

    public WitnessUserSessionService(INUserSession nUserSessionService, INtsUserSessionRepository userSessions)
    {
        _nUserSessionService = nUserSessionService;
        _userSessions = userSessions;
    }

    public async Task<NtsUserSessionStateModel?> GetCurrent()
    {
        return (await _nUserSessionService.GetCurrent<NtsUserSessionStateModel>())?.State;
    }

    public async Task SetEventId(int? eventId)
    {
        if (eventId == null)
        {
            return;
        }

        var userSession = await _nUserSessionService.GetCurrent<NtsUserSessionStateModel>();
        if (userSession == null)
        {
            return;
        }

        var currentSession = await _userSessions.ReadByUserIdentifier(userSession.UserIdentifier);
        if (currentSession?.State?.EventId == eventId)
        {
            return;
        }

        if (currentSession != null)
        {
            await _userSessions.Delete(currentSession);
        }

        var session = CreateSession(userSession, new NtsUserSessionStateModel { EventId = eventId });
        await _userSessions.Create(session);
    }

    public async Task AppendSnapshot(SnapshotGroup snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var userSession = await _nUserSessionService.GetCurrent<NtsUserSessionStateModel>();
        if (userSession == null)
        {
            return;
        }

        var currentSession = await _userSessions.ReadByUserIdentifier(userSession.UserIdentifier);
        if (currentSession == null)
        {
            currentSession = CreateSession(
                userSession,
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
        if (userSession == null)
        {
            return;
        }

        var currentSession = await _userSessions.ReadByUserIdentifier(userSession.UserIdentifier);
        if (currentSession == null)
        {
            return;
        }

        await _userSessions.Delete(currentSession);
    }

    static NtsUserSessionModel CreateSession(INUserSessionModel userSession, NtsUserSessionStateModel? state)
    {
        var session = new NtsUserSessionModel { Id = userSession.User.Id, UserIdentifier = userSession.UserIdentifier };
        session.ReplaceState(state);
        return session;
    }
}
