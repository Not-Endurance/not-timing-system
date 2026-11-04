using Microsoft.AspNetCore.Components.Routing;
using Not.Blazor.Components.Abstractions;
using NTS.Witness.Contracts.Features.Profile;

namespace NTS.Witness.Blazor.Features.Profile;

public class ProfileRouteGateBehind : NComponent
{
    [Inject]
    IWitnessProfileContext ProfileContext { get; set; } = default!;

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
            IsRedirecting = WitnessProfileRoutePolicy.ShouldRedirectToProfile(ProfileContext.User, relativePath);
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
