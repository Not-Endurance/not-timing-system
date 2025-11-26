using Not.Blazor.Components;
using Not.Blazor.Navigation;

namespace NTS.Witness.Blazor.Components.Pages;
public class AccessDeniedBehind : NComponent
{
    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected void RetrySignin()
    {
        Navigator.NavigateTo(SIGNIN, forceLoad: true);
    }
}
