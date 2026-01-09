using Not.Blazor.Components;
using Not.Notify;
using NTS.Judge.Features.Core;

namespace NTS.Judge.Blazor.Core.Dashboards;

public partial class DashboardPage : NBehind
{
    [Inject]
    ICoreService CoreService { get; set; } = default!;
    protected int? ArchiveId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await Observe(CoreService);
    }

    protected async Task LoadArchive()
    {
        if (!ArchiveId.HasValue)
        {
            NotifyHelper.Warn("Provide Archive ID");
            return;
        }
        await CoreService.LoadArchive(ArchiveId.Value);
    }
}
