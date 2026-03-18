using Microsoft.AspNetCore.Components.Authorization;
using Not.Application.Authentication.Abstractions;
using Not.Application.Authentication.User;
using Not.Application.CRUD.Ports;
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
    readonly IRepository<UserSessionModel> _sessions;
    NUserModel? _currentUser;
    string? _currentEmail;

    public WitnessUserSessionService(
        AuthenticationStateProvider authStateProvider,
        IUserRegister userRegister,
        IRepository<UserSessionModel> sessions
    )
    {
        _authStateProvider = authStateProvider;
        _userRegister = userRegister;
        _sessions = sessions;
    }

    public async Task<ICoreSession?> GetCurrent()
    {
        var user = await ResolveCurrentUser();
        if (user == null)
        {
            return null;
        }

        return await _sessions.Read(user.Id);
    }

    public async Task SetEventId(int? eventId)
    {
        var user = await ResolveCurrentUser();
        if (user == null)
        {
            return;
        }

        var session = await _sessions.Read(user.Id);
        if (session == null)
        {
            if (eventId == null)
            {
                return;
            }

            await _sessions.Create(new UserSessionModel { Id = user.Id, EventId = eventId });
            return;
        }

        ResetHistoryOnEventChange(session, eventId);
        await _sessions.Update(session);
    }

    public async Task AppendSnapshot(SnapshotGroup snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var user = await ResolveCurrentUser();
        if (user == null)
        {
            return;
        }

        var session = await _sessions.Read(user.Id);
        if (session == null)
        {
            session = new UserSessionModel { Id = user.Id, SnapshotHistory = [SnapshotGroupModel.MapFrom(snapshot)] };
            await _sessions.Create(session);
            return;
        }

        session.SnapshotHistory = [.. session.SnapshotHistory, SnapshotGroupModel.MapFrom(snapshot)];
        await _sessions.Update(session);
    }

    public async Task DeleteCurrent()
    {
        var user = await ResolveCurrentUser();
        if (user == null)
        {
            return;
        }

        var session = await _sessions.Read(user.Id);
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

    async Task<NUserModel?> ResolveCurrentUser()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var principal = authState.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            _currentUser = null;
            _currentEmail = null;
            return null;
        }

        var registration = NUserClaimsHelper.ResolveRegistration(principal);
        if (registration == null)
        {
            return null;
        }

        if (
            _currentUser != null
            && string.Equals(_currentEmail, registration.Email, StringComparison.OrdinalIgnoreCase)
        )
        {
            return _currentUser;
        }

        _currentEmail = registration.Email;
        _currentUser = null;

        var userResult = await _userRegister.Get(registration.Email);
        if (!userResult.IsError && userResult.Data != null)
        {
            _currentUser = userResult.Data;
            return _currentUser;
        }

        var registerResult = await _userRegister.Register(registration);
        if (registerResult.IsError || registerResult.Data == null)
        {
            return null;
        }

        _currentUser = registerResult.Data;
        return _currentUser;
    }
}
