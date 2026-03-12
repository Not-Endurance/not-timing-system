using Not.Application.Authentication.Abstractions;
using Not.Application.CRUD.Ports;
using Not.Blazor.Components.Abstractions;
using NTS.Application.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Setup.Aggregates;
using NTS.Witness.Features.Setup.UpcomingEvents;

namespace NTS.Witness.Blazor.Features;

public class HomeContentBehind : NComponent
{
    [Inject]
    IUpcomingEventService UpcomingEventService { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    [Inject]
    IReadMany<Participation> Participations { get; set; } = default!;

    [Inject]
    INUserSession? UserSession { get; set; } = default!;

    protected IEnumerable<UpcomingEvent> Events { get; set; } = [];
    protected string[] EventsTableHeaders { get; set; } = [Event_string, Place_string, Country_string, ""];
    protected string UserName { get; set; } = string.Empty;
    protected string UserRoles { get; set; } = string.Empty;
    protected bool IsConnected => SocketService.Event != null;
    protected UpcomingEvent? SelectedEvent => SocketService.Event;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            if (UserSession != null)
            {
                await UserSession.Initialize();
            }
            Events = await UpcomingEventService.GetEvents();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task ConnectTo(UpcomingEvent upcomingEvent)
    {
        try
        {
            if (IsConnected)
            {
                await SocketService.Disconnect();
            }

            await SocketService.Connect(upcomingEvent);
            var activeParticipations = await Participations.ReadMany(x => !x.IsComplete() && !x.IsEliminated());
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task Disconnect()
    {
        try
        {
            await SocketService.Disconnect();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
