namespace NTS.Witness.Blazor.Components.Auth.Authentication;

public class SigninBehind : ComponentBase
{
    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected void SigninHandler()
    {
        Navigator.NavigateTo(WitnessBlazorConstants.Pages.SIGNIN, forceLoad: true);
    }
}
