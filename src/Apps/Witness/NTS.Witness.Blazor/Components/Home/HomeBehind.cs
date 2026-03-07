using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Not.Blazor.Client;
using Not.Blazor.Components.Abstractions;

namespace NTS.Witness.Blazor.Components.Home;

public class HomeBehind : NComponent
{
    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    [Inject]
    AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    protected string UserName { get; set; } = string.Empty;

    protected string UserRoles { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            var roles = user.FindAll(ClaimTypes.Role);

            UserName = user.Identity?.Name ?? string.Empty;
            UserRoles = roles.Any() ? string.Join(", ", roles.Select(r => r.Value)) : No_roles_assigned_string;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void SigninHandler()
    {
        try
        {
            Navigator.NavigateTo(NBlazorContents.SIGNIN, forceLoad: true);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void SignoutHandler()
    {
        try
        {
            Navigator.NavigateTo(NBlazorContents.SIGNOUT, forceLoad: true);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
