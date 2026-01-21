using Not.Application.RPC;
using NTS.Application.Warp;
using NTS.Domain.Setup.Aggregates;
using NTS.Witness.Services;

public class EnduranceEventsBehind : ComponentBase
{
    [Inject]
    IWitnessEvents WitnessEvents { get; set; } = default!;

    [Inject]
    IRpcContext<UpcomingEvent> RpcContext { get; set; } = default!;

    [Inject]
    IRpcInitializer RpcInitializer { get; set; } = default!;

    [Inject]
    protected ISelectedEventContext Selected { get; set; } = default!;
    protected IEnumerable<UpcomingEvent> Events { get; set; } = [];
    protected string[] EventsTableHeaders { get; set; } = [Event_string, Place_string, Country_string, ""];

    protected bool IsConnected => RpcInitializer.IsConnected();

    protected override async Task OnInitializedAsync()
    {
        Events = await WitnessEvents.Get();
    }

    protected async void ConnectTo(UpcomingEvent enduranceEvent)
    {
        if (IsConnected)
        {
            await RpcContext.ResetEvent();
        }
        await RpcContext.Set(enduranceEvent);
        StateHasChanged();
    }

    protected async Task Disconnect()
    {
        await RpcContext.ResetEvent();
        StateHasChanged();
    }
}
