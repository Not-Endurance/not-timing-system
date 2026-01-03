using Not.Blazor.Components;
using Not.Blazor.Navigation;

namespace NTS.Witness.Blazor.Components.Auth.Authentication;

public class RedirectBehind : NBehind
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    protected override void OnInitialized()
    {
        try
        {
            Navigator.NavigateTo(WitnessBlazorConstants.Pages.PROFILE);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
