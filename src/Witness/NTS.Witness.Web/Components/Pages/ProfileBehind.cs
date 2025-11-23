using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace NTS.Witness.Web.Components.Pages;

public class ProfileBehind : ComponentBase 
{
    [Inject]
    NavigationManager Navigator { get; set; } = default!;
    [Inject]
    AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    protected string UserRoles { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        var roles = user.FindAll(ClaimTypes.Role);

        UserRoles = roles.Any() ? string.Join(", ", roles.Select(r => r.Value)) : "No roles assigned";
    }

    protected void SigninHandler()
    {
        Navigator.NavigateTo(SIGNIN, forceLoad: true);
    }

    protected void SignoutHandler()
    {
        Navigator.NavigateTo(SIGNOUT, forceLoad: true);
    }
}
