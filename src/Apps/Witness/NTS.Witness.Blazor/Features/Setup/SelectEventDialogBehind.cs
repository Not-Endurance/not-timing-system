using Not.Blazor.Dialogs.Abstractions;
using NTS.Application.Socket;
using NTS.Domain.Setup.Aggregates;
using NTS.Witness.Features.Setup.UpcomingEvents;

namespace NTS.Witness.Blazor.Features.Setup;

public class SelectEventDialogBehind : NDialog
{
    [Inject]
    IUpcomingEventService UpcomingEventService { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    protected IEnumerable<UpcomingEvent> Events { get; set; } = [];
    protected string[] EventsTableHeaders { get; set; } = [Event_string, Place_string, Country_string, ""];
    protected bool IsConnected => SocketService.IsConnected;
    protected UpcomingEvent? SelectedEvent => SocketService.Event;

    protected override async Task OnInitializedAsync()
    {
        try
        {
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
            if (SocketService.IsConnected && SelectedEvent?.Id != upcomingEvent.Id)
            {
                await SocketService.Disconnect();
            }

            await SocketService.Connect(upcomingEvent);
            if (SocketService.IsConnected)
            {
                await ConfirmDialog();
            }
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
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
