using Microsoft.Extensions.Options;
using Not.Application.Authentication.Abstractions;
using Not.Application.Authentication.Provider;
using Not.Injection;

namespace Not.Blazor.Client.Authentication.Services;

internal class ClientAuthenticationSessionService : INAuthenticationSession, ITransient
{
    readonly INAuthenticationSessionStorage _authenticationMarkers;
    readonly NClientAuthenticationSettings _settings;

    public ClientAuthenticationSessionService(
        INAuthenticationSessionStorage authenticationMarkers,
        IOptions<NClientAuthenticationSettings> clientAuthenticationOptions
    )
    {
        _authenticationMarkers = authenticationMarkers;
        _settings = clientAuthenticationOptions.Value;
    }

    public async Task<bool> ShouldTryAutoSignin()
    {
        var startedAt = await _authenticationMarkers.ReadSessionStartedAtAsync();
        return NClientAuthenticationSessionPolicy.IsSessionActive(startedAt, _settings, DateTimeOffset.UtcNow);
    }

    public async Task<bool> HasActiveSession()
    {
        var now = DateTimeOffset.UtcNow;
        var startedAt = await _authenticationMarkers.ReadSessionStartedAtAsync();
        if (NClientAuthenticationSessionPolicy.IsSessionActive(startedAt, _settings, now))
        {
            await _authenticationMarkers.ClearSigninFlowStartedAt();
            return true;
        }

        var flowStartedAt = await _authenticationMarkers.ReadSigninFlowStartedAtAsync();
        if (flowStartedAt.HasValue)
        {
            return true;
        }

        Clear();
        return false;
    }

    public async Task Commit()
    {
        var now = DateTimeOffset.UtcNow;
        var startedAt = await _authenticationMarkers.ReadSessionStartedAtAsync();
        if (NClientAuthenticationSessionPolicy.IsSessionActive(startedAt, _settings, now))
        {
            await _authenticationMarkers.ClearSigninFlowStartedAt();
            return;
        }

        var flowStartedAt = await _authenticationMarkers.ReadSigninFlowStartedAtAsync();
        if (flowStartedAt.HasValue)
        {
            await _authenticationMarkers.WriteSessionStartedAt(now);
            await _authenticationMarkers.ClearSigninFlowStartedAt();
            return;
        }

        Clear();
    }

    public void Clear()
    {
        _authenticationMarkers.ClearSessionStartedAt();
        _authenticationMarkers.ClearSigninFlowStartedAt();
    }
}

internal interface INAuthenticationSession
{
    Task<bool> ShouldTryAutoSignin();
    Task<bool> HasActiveSession();
    Task Commit();
    void Clear();
}
