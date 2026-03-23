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

    public WitnessUserSessionService(
        INUserSession nUserSessionService,
        INtsUserSessionRepository userSessions
    )
    {
        _nUserSessionService = nUserSessionService;
        _userSessions = userSessions;
    }

    public async Task<NtsUserSessionModel?> GetCurrent()
    {
        return (await _nUserSessionService.GetCurrent<NtsUserSessionModel>())?.State;
    }

    public async Task SetEventId(int? eventId)
    {
        if (eventId == null)
        {
            return;
        }

        var userSession = await _nUserSessionService.GetCurrent<NtsUserSessionModel>();
        if (userSession == null)
        {
            return;
        }

        var currentSession = userSession.State;
        if (currentSession?.EventId == eventId)
        {
            return;
        }

        if (currentSession != null)
        {
            await _userSessions.Delete(currentSession);
        }

        await _userSessions.Create(CreateSession(userSession, eventId: eventId));
    }

    public async Task AppendSnapshot(SnapshotGroup snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var userSession = await _nUserSessionService.GetCurrent<NtsUserSessionModel>();
        if (userSession == null)
        {
            return;
        }

        var currentSession = userSession.State;
        if (currentSession == null)
        {
            currentSession = CreateSession(userSession, snapshot: snapshot);
            await _userSessions.Create(currentSession);
            return;
        }

        currentSession.SnapshotHistory = [.. currentSession.SnapshotHistory, SnapshotGroupModel.MapFrom(snapshot)];
        await _userSessions.Update(currentSession);
    }

    public async Task DeleteCurrent()
    {
        var userSession = await _nUserSessionService.GetCurrent<NtsUserSessionModel>();
        if (userSession == null)
        {
            return;
        }

        var currentSession = userSession.State;
        if (currentSession == null)
        {
            return;
        }

        await _userSessions.Delete(currentSession);
    }

    static NtsUserSessionModel CreateSession(INUserSessionModel userSession, int? eventId = null, SnapshotGroup? snapshot = null)
    {
        return new NtsUserSessionModel
        {
            Id = userSession.User.Id,
            UserIdentifier = userSession.UserIdentifier,
            EventId = eventId,
            SnapshotHistory = snapshot == null ? [] : [SnapshotGroupModel.MapFrom(snapshot)],
        };
    }

}
