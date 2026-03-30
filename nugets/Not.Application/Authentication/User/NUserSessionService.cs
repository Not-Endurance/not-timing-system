using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Authentication.Abstractions;

namespace Not.Application.Authentication.User;

public class NUserSessionService : INUserSession
{
    readonly AuthenticationStateProvider _authStateProvider;
    readonly IServiceProvider _serviceProvider;
    readonly IUserRegister _userRegister;

    public NUserSessionService(
        AuthenticationStateProvider authStateProvider,
        IUserRegister userRegister,
        IServiceProvider serviceProvider
    )
    {
        _authStateProvider = authStateProvider;
        _userRegister = userRegister;
        _serviceProvider = serviceProvider;
    }

    public async Task<INUserSessionModel<TSessionState>?> GetCurrent<TSessionState>()
        where TSessionState : class
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var principal = authState.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var registration = NUserClaimsHelper.ResolveRegistration(principal);
        var userIdentifier = NUserClaimsHelper.ResolveUserIdentifier(principal);
        if (registration == null || string.IsNullOrWhiteSpace(userIdentifier))
        {
            return null;
        }

        var userResult = await _userRegister.Get(registration.Email);
        if (!userResult.IsError && userResult.Data != null)
        {
            return new NUserSessionModel<TSessionState>(
                userIdentifier,
                userResult.Data,
                await ResolveCurrentSession<TSessionState>(userIdentifier)
            );
        }

        var registerResult = await _userRegister.Register(registration);
        if (registerResult.IsError || registerResult.Data == null)
        {
            return null;
        }

        return new NUserSessionModel<TSessionState>(
            userIdentifier,
            registerResult.Data,
            await ResolveCurrentSession<TSessionState>(userIdentifier)
        );
    }

    async Task<TSessionState?> ResolveCurrentSession<TSessionState>(string userIdentifier)
        where TSessionState : class
    {
        var repository = _serviceProvider.GetService<INUserSessionRepository<TSessionState>>();
        if (repository == null)
        {
            return null;
        }

        return await repository.ReadByUserIdentifier(userIdentifier);
    }
}
