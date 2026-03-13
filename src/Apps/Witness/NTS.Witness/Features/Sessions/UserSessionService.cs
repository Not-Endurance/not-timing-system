using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Not.Application.Authentication.Abstractions;
using Not.Application.Authentication.User;
using Not.Application.CRUD.Ports;
using Not.Injection;
using NTS.Application.Watcher;
using NTS.Domain.Core;
using NTS.Domain.Watcher;

namespace NTS.Witness.Features.Sessions;

public class UserSessionService : IUserSessionService, IScoped
{
    readonly AuthenticationStateProvider _authStateProvider;
    readonly IUserRegister _userRegister;
    readonly IRepository<UserSessionModel> _sessions;
    NUserModel? _currentUser;
    string? _currentEmail;

    public UserSessionService(
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

        session.EventId = eventId;
        await _sessions.Update(session);
    }

    public async Task AppendSnapshot(SnapshotGroup snapshot, int? eventId = null)
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
            session = new UserSessionModel
            {
                Id = user.Id,
                EventId = eventId,
                SnapshotHistory = [SnapshotGroupModel.MapFrom(snapshot)],
            };
            await _sessions.Create(session);
            return;
        }

        if (eventId != null)
        {
            session.EventId = eventId.Value;
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

        var email = ResolveEmail(principal);
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        if (_currentUser != null && string.Equals(_currentEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            return _currentUser;
        }

        _currentEmail = email;
        _currentUser = null;

        var userResult = await _userRegister.Get(email);
        if (!userResult.IsError && userResult.Data != null)
        {
            _currentUser = userResult.Data;
            return _currentUser;
        }

        var registerResult = await _userRegister.Register(email);
        if (registerResult.IsError || registerResult.Data == null)
        {
            return null;
        }

        _currentUser = registerResult.Data;
        return _currentUser;
    }

    static string? ResolveEmail(ClaimsPrincipal principal)
    {
        var rawEmail =
            principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value
            ?? principal.FindFirst("emails")?.Value
            ?? principal.FindFirst("preferred_username")?.Value
            ?? principal.FindFirst(ClaimTypes.Upn)?.Value;

        if (string.IsNullOrWhiteSpace(rawEmail))
        {
            return null;
        }

        if (!rawEmail.StartsWith('['))
        {
            return rawEmail;
        }

        try
        {
            var emails = JsonSerializer.Deserialize<string[]>(rawEmail);
            return emails?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        }
        catch (JsonException)
        {
            return rawEmail;
        }
    }
}

public interface IUserSessionService
{
    Task<ICoreSession?> GetCurrent();
    Task SetEventId(int? eventId);
    Task AppendSnapshot(SnapshotGroup snapshot, int? eventId = null);
    Task DeleteCurrent();
}
