using Not.Blazor.Components.Abstractions;
using Not.Notify;
using NTS.Judge.Features.Core.State;

namespace NTS.Judge.Blazor.Features.Core.Dashboards;

public class DashboardContentBehind : NStatefulComponent
{
    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected int? ArchiveId { get; set; }

    [Inject]
    protected ITimingStateService Service { get; set; } = default!;

    protected async Task LoadArchive()
    {
        if (!ArchiveId.HasValue)
        {
            Notifier.Warn("Provide Archive ID");
            return;
        }
        await Service.LoadArchive(ArchiveId.Value);
    }

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
