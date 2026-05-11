using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Client.Authentication.Components;

public class AuthenticateRedirectContentBehind : NComponent
{
    bool _redirectRegisterActionToUnifiedAuthentication;

    [Inject]
    NavigationManager NavigationManager { get; set; } = default!;

    protected bool ShouldShowSpinner =>
        !RemoteAuthenticationActions.IsAction(RemoteAuthenticationActions.LogInFailed, Action ?? string.Empty)
        && !RemoteAuthenticationActions.IsAction(RemoteAuthenticationActions.LogOutFailed, Action ?? string.Empty)
        && !RemoteAuthenticationActions.IsAction(RemoteAuthenticationActions.LogOutSucceeded, Action ?? string.Empty);

    protected bool ShouldRenderRemoteAuthenticator => !_redirectRegisterActionToUnifiedAuthentication;

    [Parameter]
    public string? Action { get; set; }

    protected override void OnParametersSet()
    {
        _redirectRegisterActionToUnifiedAuthentication = false;

        if (string.IsNullOrWhiteSpace(Action))
        {
            Action = RemoteAuthenticationActions.LogIn;
            return;
        }

        if (string.Equals(Action, RemoteAuthenticationActions.Register, StringComparison.OrdinalIgnoreCase))
        {
            _redirectRegisterActionToUnifiedAuthentication = true;
        }
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !_redirectRegisterActionToUnifiedAuthentication)
        {
            return Task.CompletedTask;
        }

        NavigationManager.NavigateTo(AuthenticationContents.AUTHENTICATION, replace: true);
        return Task.CompletedTask;
    }
}
