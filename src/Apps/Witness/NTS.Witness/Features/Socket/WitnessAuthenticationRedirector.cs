using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Not.Injection;

namespace NTS.Witness.Features.Socket;

public interface IWitnessAuthenticationRedirector
{
    void RedirectToSignIn(string scope, string returnUrl);
}

public class WitnessAuthenticationRedirector : IWitnessAuthenticationRedirector, IScoped
{
    readonly NavigationManager _navigationManager;

    public WitnessAuthenticationRedirector(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    public void RedirectToSignIn(string scope, string returnUrl)
    {
        var requestOptions = new InteractiveRequestOptions
        {
            Interaction = InteractionType.GetToken,
            ReturnUrl = returnUrl,
            Scopes = [scope],
        };

        _navigationManager.NavigateToLogin(RemoteAuthenticationDefaults.LoginPath, requestOptions);
    }
}
