using Not.Blazor.Components;
using Not.Notify;
using NTS.Judge.Blazor.Shared.Components.SidePanels;

namespace NTS.Judge.Blazor.Core.Dashboards;

public partial class DashboardPage : NComponent
{
    [Inject]
    ICoreBehind CoreBehind { get; set; } = default!;
    protected int? ArchiveId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await Observe(CoreBehind);
    }

    protected async Task LoadArchive()
    {
        if (!ArchiveId.HasValue)
        {
            NotifyHelper.Warn("Provide Archive ID");
            return;
        }
        await CoreBehind.LoadArchive(ArchiveId.Value);
    }
}
