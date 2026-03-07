using Not.Application.Authentication.Abstractions;

namespace Not.Blazor.Auth;

internal class ClientSideBlazorAuthenticationService : INAuthentication
{
    readonly NavigationManager _navigationManager;

    public ClientSideBlazorAuthenticationService(NavigationManager navigationManager)
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
