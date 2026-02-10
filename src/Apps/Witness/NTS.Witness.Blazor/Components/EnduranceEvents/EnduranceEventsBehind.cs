using Not.Application.CRUD.Ports;
using NTS.Application.Socket;
using NTS.Domain.Setup.Aggregates;
using NTS.Witness.Services;

namespace NTS.Witness.Blazor.Components.EnduranceEvents;

public class EnduranceEventsBehind : ComponentBase
{
    [Inject]
    IRepository<UpcomingEvent> Repository { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    [Inject]
    IConnectionStatus ConnectionStatus { get; set; } = default!;

    [Inject]
    IParticipationGetter ParticipationGetter { get; set; } = default!;

    [Inject]
    protected INtsSocketService Selected { get; set; } = default!;
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
            await SocketService.Disconnect();
        }
        await SocketService.Connect(enduranceEvent);
        await ParticipationGetter.GetParticipations();
        StateHasChanged();
    }

    protected async Task Disconnect()
    {
        await SocketService.Disconnect();
        StateHasChanged();
    }
}
