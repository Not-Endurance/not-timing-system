using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Not.Injection;

namespace NTS.Witness.Features.Socket;

public interface IWitnessAuthenticationRedirector
{
    void RedirectToSignIn(AccessTokenResult tokenResult);
    void RedirectToSignIn(AccessTokenNotAvailableException exception);
    void RedirectToSignIn(InteractiveRequestOptions requestOptions);
}

public class WitnessAuthenticationRedirector : IWitnessAuthenticationRedirector, IScoped
{
    readonly NavigationManager _navigationManager;

    public WitnessAuthenticationRedirector(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    public void RedirectToSignIn(AccessTokenResult tokenResult)
    {
        var requestUrl = string.IsNullOrWhiteSpace(tokenResult.InteractiveRequestUrl)
            ? RemoteAuthenticationDefaults.LoginPath
            : tokenResult.InteractiveRequestUrl;
        var requestOptions =
            tokenResult.InteractionOptions
            ?? new InteractiveRequestOptions
            {
                Interaction = InteractionType.GetToken,
                ReturnUrl = _navigationManager.Uri,
            };
        _navigationManager.NavigateToLogin(requestUrl, requestOptions);
    }

    public void RedirectToSignIn(AccessTokenNotAvailableException exception)
    {
        exception.Redirect();
    }

    public void RedirectToSignIn(InteractiveRequestOptions requestOptions)
    {
        _navigationManager.NavigateToLogin(RemoteAuthenticationDefaults.LoginPath, requestOptions);
    }
}
