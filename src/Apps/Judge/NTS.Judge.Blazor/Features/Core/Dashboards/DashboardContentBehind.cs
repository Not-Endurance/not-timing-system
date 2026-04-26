using Not.Blazor.Components.Abstractions;
using NTS.Application.Contracts.Socket;
using NTS.Judge.Contracts.Features.Core;

namespace NTS.Judge.Blazor.Features.Core.Dashboards;

public class DashboardContentBehind : NStatefulComponent
{
    protected bool HasActiveEvent => SocketService.Event != null;

    [Inject]
    protected IDashService Service { get; set; } = default!;

    [Inject]
    protected INtsSocketService SocketService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(SocketService);
    }
}
