using NC.Blazor.Components.Abstractions;
using NoTiming.Application.Contracts.Socket;

namespace NoTiming.Judge.Blazor.Features.Core.Dashboards;

public class DashboardContentBehind : NStatefulComponent
{
    protected bool HasActiveEvent => SocketService.Event != null;

    [Inject]
    protected INoTimingSocketService SocketService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(SocketService);
    }
}
