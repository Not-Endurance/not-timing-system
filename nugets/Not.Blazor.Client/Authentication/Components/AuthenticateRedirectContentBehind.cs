using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Client.Authentication.Components;

public class AuthenticateRedirectContentBehind : NComponent
{
    [Parameter]
    public string? Action { get; set; }

    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(Action))
        {
            Action = RemoteAuthenticationActions.LogIn;
        }
    }
}
