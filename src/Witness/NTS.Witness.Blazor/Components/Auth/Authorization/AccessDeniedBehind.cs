using Not.Blazor.Components;

namespace NTS.Witness.Blazor.Components.Auth.Authorization;

public class AccessDeniedBehind : NComponent
{
    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected void RetrySignin()
    {
        Navigator.NavigateTo(WitnessBlazorConstants.Pages.SIGNIN, forceLoad: true);
    }
}
