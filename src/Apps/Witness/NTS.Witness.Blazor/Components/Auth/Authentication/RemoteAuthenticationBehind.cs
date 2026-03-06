using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace NTS.Witness.Blazor.Components.Auth.Authentication;

public class RemoteAuthenticationBehind : ComponentBase
{
    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected string Action { get; set; } = RemoteAuthenticationActions.LogIn;

    protected override void OnInitialized()
    {
        var endpoint = ResolveEndpoint();
        Action = endpoint switch
        {
            WitnessBlazorConstants.Pages.SIGNIN => RemoteAuthenticationActions.LogIn,
            WitnessBlazorConstants.Pages.SIGNIN_CALLBACK => RemoteAuthenticationActions.LogInCallback,
            WitnessBlazorConstants.Pages.SIGNIN_CALLBACK_ALT => RemoteAuthenticationActions.LogInCallback,
            WitnessBlazorConstants.Pages.SIGNOUT => RemoteAuthenticationActions.LogOut,
            WitnessBlazorConstants.Pages.SIGNOUT_CALLBACK => RemoteAuthenticationActions.LogOutCallback,
            WitnessBlazorConstants.Pages.SIGNOUT_CALLBACK_ALT => RemoteAuthenticationActions.LogOutCallback,
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
