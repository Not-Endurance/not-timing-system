using Not.Safe;
using NTS.Witness.RPC;

namespace NTS.Witness.Components.Pages;

public class WitnessPageBehind : ComponentBase
{
    [Inject]
    ITestBehind TestBehind { get; set; } = default!;
    protected bool IsUserOfficial { get; set; } = false;
    protected bool IsUserLoggedIn { get; set; } = false;

    protected override async void OnInitialized()
    {
        try
        {
            TestBehind.Test();
        }
        catch (Exception ex)
        {
            await SafeHelper.HandleException(ex);
        }
    }
}
