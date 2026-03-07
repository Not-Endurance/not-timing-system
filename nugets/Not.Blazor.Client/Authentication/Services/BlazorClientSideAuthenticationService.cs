using Microsoft.AspNetCore.Components;
using Not.Application.Authentication.Abstractions;

namespace Not.Blazor.Client.Authentication.Services;

internal class BlazorClientSideAuthenticationService : INAuthentication
{
    readonly NavigationManager _navigationManager;

    public BlazorClientSideAuthenticationService(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    public void Signin()
    {
        _navigationManager.NavigateTo(NBlazorContents.SIGNIN, forceLoad: true);
    }

    public void Signout()
    {
        _navigationManager.NavigateTo(NBlazorContents.SIGNOUT, forceLoad: true);
    }
}
