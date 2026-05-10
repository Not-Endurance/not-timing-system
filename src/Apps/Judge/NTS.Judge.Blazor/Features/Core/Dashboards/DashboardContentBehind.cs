using Not.Blazor.Components.Abstractions;
using NTS.Application.Contracts.Socket;

namespace NTS.Judge.Blazor.Features.Core.Dashboards;

public class DashboardContentBehind : NStatefulComponent
{
    protected bool HasActiveEvent => SocketService.Event != null;

    [Inject]
    protected INtsSocketService SocketService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(SocketService);
    }
}
