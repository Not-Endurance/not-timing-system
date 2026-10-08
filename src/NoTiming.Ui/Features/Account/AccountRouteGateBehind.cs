using Microsoft.AspNetCore.Components.Routing;
using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Account;

namespace NoTiming.Ui.Features.Account;

/// <summary>
/// Holds a page back until the person is known to be allowed it (<see cref="WitnessRoutePolicy"/>): a visitor who asks for
/// a page of signed-in people is taken to the host's sign-in page and back to the page, and a person without a complete
/// profile who asks to send Snapshots is taken to complete it. A page that asks nothing is shown at once and does not wait
/// for the host to say who is signed in; a host that does not say lets the page be shown, as it cannot be told who is there
/// and the host decides what the page may do.
/// </summary>
public class AccountRouteGateBehind : NComponent
{
    [Inject]
    IAccountSession Account { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected bool IsShown { get; private set; }

    [Parameter]
    public RouteData RouteData { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            var relativePath = Navigator.ToBaseRelativePath(Navigator.Uri);
            IsShown = WitnessRoutePolicy.AccessTo(null, relativePath) == RouteAccess.Open;
            if (IsShown)
            {
                return;
            }

            await Account.Load();
            var access = Account.IsKnown
                ? WitnessRoutePolicy.AccessTo(Account.Current, relativePath)
                : RouteAccess.Open;
            switch (access)
            {
                case RouteAccess.SignIn:
                    Navigator.NavigateTo(AccountSession.SignInUrl($"/{relativePath}"), forceLoad: true);
                    break;
                case RouteAccess.Profile:
                    Navigator.NavigateTo(Routes.PROFILE_PAGE, replace: true);
                    break;
                default:
                    IsShown = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
