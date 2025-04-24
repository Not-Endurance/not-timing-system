using Microsoft.AspNetCore.Components;
using NTS.Witness.RPC;

namespace NTS.Witness.Components.Pages;

public partial class WitnessPage
{
    bool IsUserOfficial { get; set; } = false;

    bool IsUserLoggedIn { get; set; } = false;

    [Inject]
    ITestBehind TestBehind { get; set; } = default!;

    protected override void OnInitialized()
    {
        TestBehind.Test();
    }
}
