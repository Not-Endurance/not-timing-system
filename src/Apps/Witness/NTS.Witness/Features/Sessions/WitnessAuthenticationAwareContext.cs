using Microsoft.AspNetCore.Components.Authorization;
using Not.Application.Behinds.Adapters;

namespace NTS.Witness.Features.Sessions;

/// <summary>
/// State derived from the signed-in user is loaded once and cached, but the auth state itself
/// settles after the app has already booted: the sign-in round trip returns the browser to a
/// freshly loaded, still anonymous app and only completes the callback afterwards. A context that
/// does not reload on that change keeps serving the anonymous snapshot it was initialized with
/// until the next full page load, which is what left the drawer without its profile header.
/// </summary>
public abstract class WitnessAuthenticationAwareContext : NStatefulService
{
    readonly AuthenticationStateProvider _authenticationStateProvider;

    protected WitnessAuthenticationAwareContext(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _authenticationStateProvider.AuthenticationStateChanged += HandleAuthenticationStateChanged;
    }

    public override void Dispose()
    {
        _authenticationStateProvider.AuthenticationStateChanged -= HandleAuthenticationStateChanged;
        base.Dispose();
    }

    void HandleAuthenticationStateChanged(Task<AuthenticationState> authenticationState)
    {
        // The provider raises this synchronously and has nowhere to await the reload, so the task
        // runs on its own. Load already routes its own failures through the safe handler.
        _ = ReloadState();
    }
}
