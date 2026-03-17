using Microsoft.AspNetCore.Components;
using Not.Application.Authentication.Abstractions;
using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Client.Authentication.Components;

public abstract class AuthenticateContentBehind : NComponent
{
    [Inject]
    INAuthentication Authentication { get; set; } = default!;

    protected void Signin()
    {
        try
        {
            Authentication.Signin();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void Register()
    {
        try
        {
            Authentication.Register();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
