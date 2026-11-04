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
        _navigationManager.NavigateTo(AuthenticationContents.AUTHENTICATION);
    }
}
