using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using NC.Injection;

namespace NoTiming.Ui.Application.Features.Socket;

public interface IUiAuthenticationRedirector
{
    void RedirectToSignIn(AccessTokenResult tokenResult);
    void RedirectToSignIn(AccessTokenNotAvailableException exception);
    void RedirectToSignIn(InteractiveRequestOptions requestOptions);
}

public class UiAuthenticationRedirector : IUiAuthenticationRedirector, IScoped
{
    readonly NavigationManager _navigationManager;

    public UiAuthenticationRedirector(NavigationManager navigationManager)
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
