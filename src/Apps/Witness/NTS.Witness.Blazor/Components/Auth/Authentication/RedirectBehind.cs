using Not.Blazor.Components.Abstractions;
using Not.Blazor.Navigation.Abstractions;

namespace NTS.Witness.Blazor.Components.Auth.Authentication;

public class RedirectBehind : NComponent
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
