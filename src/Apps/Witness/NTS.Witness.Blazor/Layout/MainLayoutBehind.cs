using Not.Application.Authentication.Abstractions;
using Not.Blazor.Navigation.Abstractions;
using static NTS.Witness.Blazor.Routes;

namespace NTS.Witness.Blazor.Layout;

public class MainLayoutBehind : LayoutComponentBase
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    [Inject]
    INAuthentication Authentication { get; set; } = default!;

    protected void HelpHandler()
    {
        Navigator.NavigateTo(EMERGENCY_CONTACTS_PAGE);
    }

    protected Task Signout()
    {
        Authentication.Signout();
        return Task.CompletedTask;
    }
}
