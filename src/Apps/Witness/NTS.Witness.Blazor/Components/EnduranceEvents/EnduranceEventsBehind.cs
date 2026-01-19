using Not.Application.RPC;
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
    protected IEnumerable<UpcomingEvent> Events { get; set; } = [];
    protected string[] EventsTableHeaders { get; set; } = [Event_string, Place_string, Country_string, ""];

    protected bool IsConnected { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        Events = await WitnessEvents.Get();
    }

    protected async void ConnectTo(UpcomingEvent selectedEvent)
    {
        await RpcContext.Set(selectedEvent);
        await RpcInitializer.StartConnection();
        IsConnected = RpcInitializer.IsConnected();
        StateHasChanged();
    }

    protected async void Disconnect()
    {
        await RpcInitializer.Disconnect();
        IsConnected = RpcInitializer.IsConnected();
        StateHasChanged();
    }
}
