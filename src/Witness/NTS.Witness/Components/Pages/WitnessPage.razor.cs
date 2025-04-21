using Microsoft.AspNetCore.Components;
using NTS.Witness.RPC;

namespace NTS.Witness.Components.Pages;

    public partial class WitnessPage
    {
        [Inject]
        ITestBehind TestBehind { get; set; } = default!;

        protected override void OnInitialized()
        {
            TestBehind.Test();
        }
    }

