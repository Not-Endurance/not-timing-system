using Not.Blazor.Navigation;

namespace NTS.Witness.Blazor.Components;

public class SigninBehind : ComponentBase
{
    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected void SigninHandler()
    {
        Navigator.NavigateTo(SIGNIN, forceLoad: true);
    }
}
