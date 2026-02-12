using Not.Blazor.Components;
using Not.Notify;
using NTS.Judge.Features.Core.State;

namespace NTS.Judge.Blazor.Features.Core.Dashboards;

public class DashboardContentBehind : NStatefulComponent
{
    protected int? ArchiveId { get; set; }

    [Inject]
    protected ITimingStateService Service { get; set; } = default!;

    protected async Task LoadArchive()
    {
        if (!ArchiveId.HasValue)
        {
            NotifyHelper.Warn("Provide Archive ID");
            return;
        }
        await Service.LoadArchive(ArchiveId.Value);
    }

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
