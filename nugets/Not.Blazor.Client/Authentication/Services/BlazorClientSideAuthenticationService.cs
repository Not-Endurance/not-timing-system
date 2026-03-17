using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Not.Application.Authentication.Abstractions;

namespace Not.Blazor.Client.Authentication.Services;

internal class BlazorClientSideAuthenticationService : INAuthentication
{
    readonly ILocalStorageMarkerService _localStorageMarkerService;
    readonly NavigationManager _navigationManager;

    public BlazorClientSideAuthenticationService(
        ILocalStorageMarkerService localStorageMarkerService,
        NavigationManager navigationManager
    )
    {
        _localStorageMarkerService = localStorageMarkerService;
        _navigationManager = navigationManager;
    }

    public void Signin()
    {
        _localStorageMarkerService.ClearSignedOut();
        var requestOptions = new InteractiveRequestOptions { Interaction = InteractionType.SignIn, ReturnUrl = "/" };
        _navigationManager.NavigateToLogin(RemoteAuthenticationDefaults.LoginPath, requestOptions);
    }

    public void Register()
    {
        // When using Azure external Tenant Id and MSAL the application url is the same both. The User then has to click on "Create account"
        Signin();
    }

    public void Signout()
    {
        _localStorageMarkerService.SetSignedOut();
        _navigationManager.NavigateTo(AuthenticationContents.AUTHENTICATION);
    }
}
