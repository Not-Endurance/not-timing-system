using Microsoft.AspNetCore.Components;
using Not.Application.Authentication.Abstractions;
using Not.Blazor.Client.Authentication.Services;
using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Client.Authentication.Components;

public abstract class AuthenticateContentBehind : NComponent
{
    bool _hasAttemptedSilentSignin;

    [Inject]
    INAuthentication Authentication { get; set; } = default!;

    [Inject]
    INAuthenticationSession AuthenticationSession { get; set; } = default!;

    protected async Task Signin()
    {
        await Authentication.Signin();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _hasAttemptedSilentSignin)
        {
            return;
        }

        _hasAttemptedSilentSignin = true;
        if (await AuthenticationSession.ShouldTryAutoSignin())
        {
            await Authentication.Signin(silent: true);
        }
    }
}
