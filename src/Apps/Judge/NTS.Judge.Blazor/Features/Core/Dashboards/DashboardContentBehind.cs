using Not.Blazor.Components.Abstractions;
using Not.Notify;
using NTS.Application.Socket;
using NTS.Judge.Contracts.Features.Core;

namespace NTS.Judge.Blazor.Features.Core.Dashboards;

public class DashboardContentBehind : NStatefulComponent
{
    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected int? ArchiveId { get; set; }
    protected bool IsArchiveLoading { get; set; }
    protected bool HasActiveEvent => SocketService.Event != null;

    [Inject]
    protected IDashService Service { get; set; } = default!;

    [Inject]
    protected INtsSocketService SocketService { get; set; } = default!;

    protected async Task LoadArchive()
    {
        if (!ArchiveId.HasValue)
        {
            Notifier.Warn(Provide_Archive_ID_string);
            return;
        }

        try
        {
            IsArchiveLoading = true;
            await Service.LoadArchive(ArchiveId.Value);
        }
        finally
        {
            IsArchiveLoading = false;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await Observe(SocketService);
    }
}
