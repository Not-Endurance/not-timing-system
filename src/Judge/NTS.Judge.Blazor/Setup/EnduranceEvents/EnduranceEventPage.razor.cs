using Not.Application.RPC.SignalR;
using Not.Blazor.Navigation;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents;

public partial class EnduranceEventPage
{
    EnduranceEventFormModel? _upcomingEvent;
    
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    [Inject]
    IRpcSocket RpcSocket { get; set; } = default!;
    
    protected override void OnInitialized()
    {
        _upcomingEvent = Navigator.ConsumeParameter<EnduranceEventFormModel>();
    }

    protected override async Task OnInitializedAsync()
    {
        await RpcSocket.Connect();
    }
}
