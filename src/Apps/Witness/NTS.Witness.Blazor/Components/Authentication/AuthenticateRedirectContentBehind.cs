using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Not.Blazor;

namespace NTS.Witness.Blazor.Components.Authentication;

public class AuthenticateRedirectContentBehind : ComponentBase
{
    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected string Action { get; set; } = RemoteAuthenticationActions.LogIn;

    protected override void OnInitialized()
    {
        var endpoint = ResolveEndpoint();
        Action = endpoint switch
        {
            NBlazorContents.SIGNIN => RemoteAuthenticationActions.LogIn,
            NBlazorContents.SIGNIN_CALLBACK => RemoteAuthenticationActions.LogInCallback,
            NBlazorContents.SIGNIN_CALLBACK_ALT => RemoteAuthenticationActions.LogInCallback,
            NBlazorContents.SIGNOUT => RemoteAuthenticationActions.LogOut,
            NBlazorContents.SIGNOUT_CALLBACK => RemoteAuthenticationActions.LogOutCallback,
            NBlazorContents.SIGNOUT_CALLBACK_ALT => RemoteAuthenticationActions.LogOutCallback,
            _ => RemoteAuthenticationActions.LogIn,
        };
    }

    string ResolveEndpoint()
    {
        var endpoint = Navigator.ToBaseRelativePath(Navigator.Uri);
        endpoint = endpoint.Split('?')[0].Split('#')[0].Trim('/');
        return $"/{endpoint}";
    }
}
