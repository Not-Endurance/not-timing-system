using Not.Blazor.Components;
using NTS.Judge.Blazor.Shared.Components.SidePanels;

namespace NTS.Judge.Blazor.Core.Dashboards;

public partial class DashboardPage : NComponent
{
    [Inject]
    ICoreBehind CoreBehind { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(CoreBehind);
    }
}
