using Microsoft.AspNetCore.Components.Routing;
using NC.Blazor.Components.Abstractions;
using NoTiming.Ui.Contracts.Features.Profile;

namespace NoTiming.Ui.Blazor.Features.Profile;

public class ProfileRouteGateBehind : NComponent
{
    [Inject]
    IUiProfileContext ProfileContext { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected bool IsRedirecting { get; private set; }

    [Parameter]
    public RouteData RouteData { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            await ProfileContext.Load();
            var relativePath = Navigator.ToBaseRelativePath(Navigator.Uri);
            IsRedirecting = UiProfileRoutePolicy.ShouldRedirectToProfile(ProfileContext.User, ProfileContext.Profile, relativePath);
            if (IsRedirecting)
            {
                Navigator.NavigateTo(Routes.PROFILE_PAGE, replace: true);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
