using Not.Application.RPC;
using Not.Blazor.Components;
using NTS.Domain.Setup.Aggregates;
using NTS.Witness.Services;

public class EnduranceEventsBehind : ComponentBase
{
    protected IEnumerable<UpcomingEvent> Events = [];

    [Inject]
    IWitnessEvents WitnessEvents { get; set; } = default!;

    [Inject]
    IRpcContext<UpcomingEvent> RpcContext { get; set; } = default!;

    [Inject]
    IRpcInitializer RpcInitializer { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        Events = await WitnessEvents.Get();
        await RpcContext.Set(Events.First());
        await RpcInitializer.StartConnection();
    }

}
