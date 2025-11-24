using Not.Blazor.Components;
using Not.Blazor.Navigation;
using NTS.Witness.Web.RPC;

namespace NTS.Witness.Web.Components.Pages;

public class WitnessPageBehind : NComponent
{
    [Inject]
    ITestBehind TestBehind { get; set; } = default!;

    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    protected override void OnInitialized()
    {
        try
        {
            TestBehind.Test();
            Navigator.NavigateTo(PROFILE);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
