using Not.Application.CRUD.Ports;
using NTS.Application.SignalR;
using NTS.Domain.Setup.Aggregates;
using NTS.Witness.Services;

namespace NTS.Witness.Blazor.Components.EnduranceEvents;

public class EnduranceEventsBehind : ComponentBase
{
    [Inject]
    IRepository<UpcomingEvent> Repository { get; set; } = default!;

    [Inject]
    ISocketContext<UpcomingEvent> RpcContext { get; set; } = default!;

    [Inject]
    IConnectionStatus ConnectionStatus { get; set; } = default!;

    [Inject]
    IParticipationGetter ParticipationGetter { get; set; } = default!;

    [Inject]
    protected ISelectedEventContext Selected { get; set; } = default!;
    protected IEnumerable<UpcomingEvent> Events { get; set; } = [];
    protected string[] EventsTableHeaders { get; set; } = [Event_string, Place_string, Country_string, ""];

    protected bool IsConnected => ConnectionStatus.IsConnected();

    protected override async Task OnInitializedAsync()
    {
        Events = await Repository.ReadMany();
    }

    protected async void ConnectTo(UpcomingEvent enduranceEvent)
    {
        if (IsConnected)
        {
            await RpcContext.Disconnect();
        }
        await RpcContext.Connect(enduranceEvent);
        await ParticipationGetter.GetParticipations();
        StateHasChanged();
    }

    protected async Task Disconnect()
    {
        await RpcContext.Disconnect();
        StateHasChanged();
    }
}
