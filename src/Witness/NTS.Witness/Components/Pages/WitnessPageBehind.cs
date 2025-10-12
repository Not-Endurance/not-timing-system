using Not.Blazor.Components;
using NTS.Witness.RPC;

namespace NTS.Witness.Components.Pages;

public class WitnessPageBehind : NComponent
{
    [Inject]
    ITestBehind TestBehind { get; set; } = default!;
    protected bool IsUserOfficial { get; set; } = false;
    protected bool IsUserLoggedIn { get; set; } = false;

    protected override void OnInitialized()
    {
        try
        {
            TestBehind.Test();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
