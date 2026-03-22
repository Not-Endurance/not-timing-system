using Microsoft.AspNetCore.Components.Authorization;
using Not.Application.Authentication.Abstractions;
using Not.Application.Authentication.User;
using Not.Injection;
using NTS.Application.UserSession;
using NTS.Application.Watcher;
using NTS.Domain.Core;
using NTS.Domain.Watcher;

namespace NTS.Witness.Features.Sessions;

public class WitnessUserSessionService : IUserSessionService, IScoped
{
    readonly AuthenticationStateProvider _authStateProvider;
    readonly IUserRegister _userRegister;
    readonly IUserSessionRepository _sessions;
    NUserModel? _currentUser;
    string? _currentUserIdentifier;
    string? _currentEmail;

    public WitnessUserSessionService(
        AuthenticationStateProvider authStateProvider,
        IUserRegister userRegister,
        IUserSessionRepository sessions
    )
    {
        _authStateProvider = authStateProvider;
        _userRegister = userRegister;
        _sessions = sessions;
    }

    public async Task<ICoreSession?> GetCurrent()
    {
        var context = await ResolveCurrentUserContext();
        if (context == null)
        {
            return null;
        }

        return await ResolveCurrentSession(context);
    }

    public async Task SetEventId(int? eventId)
    {
        var context = await ResolveCurrentUserContext();
        if (context == null)
        {
            return;
        }

        var session = await ResolveCurrentSession(context);
        if (session == null)
        {
            if (eventId == null)
            {
                return;
            }

            await _sessions.Create(CreateSession(context, eventId: eventId));
            return;
        }

        ResetHistoryOnEventChange(session, eventId);
        await _sessions.Update(session);
    }

    public async Task AppendSnapshot(SnapshotGroup snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var context = await ResolveCurrentUserContext();
        if (context == null)
        {
            return;
        }

        var session = await ResolveCurrentSession(context);
        if (session == null)
        {
            session = CreateSession(context, snapshot: snapshot);
            await _sessions.Create(session);
            return;
        }

        session.SnapshotHistory = [.. session.SnapshotHistory, SnapshotGroupModel.MapFrom(snapshot)];
        await _sessions.Update(session);
    }

    public async Task DeleteCurrent()
    {
        var context = await ResolveCurrentUserContext();
        if (context == null)
        {
            return;
        }

        var session = await ResolveCurrentSession(context);
        if (session == null)
        {
            return;
        }

        await _sessions.Delete(session);
    }

    static void ResetHistoryOnEventChange(UserSessionModel session, int? eventId)
    {
        if (eventId == null)
        {
            return;
        }

        if (session.EventId != null && session.EventId != eventId)
        {
            session.SnapshotHistory = [];
        }

        session.EventId = eventId;
    }

    static UserSessionModel CreateSession(ResolvedUserContext context, int? eventId = null, SnapshotGroup? snapshot = null)
    {
        return new UserSessionModel
        {
            Id = context.User.Id,
            UserIdentifier = context.UserIdentifier,
            EventId = eventId,
            SnapshotHistory = snapshot == null ? [] : [SnapshotGroupModel.MapFrom(snapshot)],
        };
    }

    void ClearCurrentUserCache()
    {
        _currentUser = null;
        _currentUserIdentifier = null;
        _currentEmail = null;
    }

    async Task<UserSessionModel?> ResolveCurrentSession(ResolvedUserContext context)
    {
        var session = await _sessions.ReadByUserIdentifier(context.UserIdentifier);
        if (session != null)
        {
            return session;
        }

        session = await _sessions.Read(context.User.Id);
        if (session == null)
        {
            return null;
        }

        if (
            !string.IsNullOrWhiteSpace(session.UserIdentifier)
            && !string.Equals(session.UserIdentifier, context.UserIdentifier, StringComparison.Ordinal)
        )
        {
            return null;
        }

        if (!string.Equals(session.UserIdentifier, context.UserIdentifier, StringComparison.Ordinal))
        {
            session.UserIdentifier = context.UserIdentifier;
            await _sessions.Update(session);
        }

        return session;
    }

    async Task<ResolvedUserContext?> ResolveCurrentUserContext()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var principal = authState.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            ClearCurrentUserCache();
            return null;
        }

        var registration = NUserClaimsHelper.ResolveRegistration(principal);
        var userIdentifier = NUserClaimsHelper.ResolveUserIdentifier(principal);
        if (registration == null || string.IsNullOrWhiteSpace(userIdentifier))
        {
            ClearCurrentUserCache();
            return null;
        }

        if (
            _currentUser != null
            && string.Equals(_currentUserIdentifier, userIdentifier, StringComparison.Ordinal)
            && string.Equals(_currentEmail, registration.Email, StringComparison.OrdinalIgnoreCase)
        )
        {
            return new ResolvedUserContext(userIdentifier, _currentUser);
        }

        _currentUserIdentifier = userIdentifier;
        _currentEmail = registration.Email;
        _currentUser = null;

        var userResult = await _userRegister.Get(registration.Email);
        if (!userResult.IsError && userResult.Data != null)
        {
            _currentUser = userResult.Data;
            return new ResolvedUserContext(userIdentifier, _currentUser);
        }

        var registerResult = await _userRegister.Register(registration);
        if (registerResult.IsError || registerResult.Data == null)
        {
            return null;
        }

        _currentUser = registerResult.Data;
        return new ResolvedUserContext(userIdentifier, _currentUser);
    }

    sealed class ResolvedUserContext
    {
        public ResolvedUserContext(string userIdentifier, NUserModel user)
        {
            UserIdentifier = userIdentifier;
            User = user;
        }

        public string UserIdentifier { get; }
        public NUserModel User { get; }
    }
}
