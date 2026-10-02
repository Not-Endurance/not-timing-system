using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Not.Application.Authentication.Abstractions;

namespace Not.Blazor.Client.Authentication.Services;

internal class NBlazorClientAuthenticationService : INAuthentication
{
    readonly INAuthenticationSessionStorage _authenticationMarkers;
    readonly INAuthenticationSession _clientAuthenticationSessionService;
    readonly NavigationManager _navigationManager;

    public NBlazorClientAuthenticationService(
        INAuthenticationSessionStorage authenticationMarkers,
        INAuthenticationSession clientAuthenticationSessionService,
        NavigationManager navigationManager
    )
    {
        _authenticationMarkers = authenticationMarkers;
        _clientAuthenticationSessionService = clientAuthenticationSessionService;
        _navigationManager = navigationManager;
    }

    public async Task Signin(bool silent = false)
    {
        await _authenticationMarkers.WriteSigninFlowStartedAt();

        var requestOptions = new InteractiveRequestOptions { Interaction = InteractionType.SignIn, ReturnUrl = "/" };
        if (silent)
        {
            requestOptions.TryAddAdditionalParameter("prompt", "none");
        }

        _navigationManager.NavigateToLogin(RemoteAuthenticationDefaults.LoginPath, requestOptions);
    }

    public async Task Signout()
    {
        await _clientAuthenticationSessionService.Clear();

        // Clearing the local session markers is not enough on its own: AuthenticationStateProvider
        // keeps serving its cached authenticated user until MSAL itself signs out, so everything
        // reading the auth state (profile headers, access levels, token providers) stays signed in.
        // Going through the logout route flips the auth state instead of just hiding it.
        _navigationManager.NavigateToLogout(RemoteAuthenticationDefaults.LogoutPath, ResolveReturnUrl());
    }

    /// <summary>
    /// Sign-out returns the visitor to the page they were on, which on a public surface stays
    /// readable and now offers a sign-in affordance. Signing out from an authentication route
    /// itself would return into the same round trip, so that falls back to the app root.
    /// </summary>
    string ResolveReturnUrl()
    {
        var currentUri = _navigationManager.Uri;
        return AuthenticationContents.IsAuthenticationRoute(currentUri) ? _navigationManager.BaseUri : currentUri;
    }
}
