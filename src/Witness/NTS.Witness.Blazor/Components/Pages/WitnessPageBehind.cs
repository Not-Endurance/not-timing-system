using Not.Blazor.Components;
using Not.Blazor.Navigation;
using NTS.Witness.RPC;

namespace NTS.Witness.Blazor.Components.Pages;

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
