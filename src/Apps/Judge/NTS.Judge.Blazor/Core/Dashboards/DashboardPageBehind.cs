using Not.Blazor.Components;
using Not.Notify;
using NTS.Judge.Features.Core;

namespace NTS.Judge.Blazor.Core.Dashboards;

public class DashboardPageBehind : NStatefulComponent<ICoreService>
{
    protected int? ArchiveId { get; set; }

    protected async Task LoadArchive()
    {
        if (!ArchiveId.HasValue)
        {
            NotifyHelper.Warn("Provide Archive ID");
            return;
        }
        await Service.LoadArchive(ArchiveId.Value);
    }
}
