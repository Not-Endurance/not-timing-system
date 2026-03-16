using MudBlazor;
using Not.Blazor.Dialogs.Abstractions;
using Not.Blazor.Helpers;
using NTS.Application.Socket;
using NTS.Domain.Setup.Aggregates;
using NTS.Witness.Features.Sessions;
using NTS.Witness.Features.Setup.UpcomingEvents;

namespace NTS.Witness.Blazor.Features.Setup;

public class SelectEventDialogBehind : NDialog
{
    [Inject]
    IUpcomingEventService UpcomingEventService { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    [Inject]
    IUserSessionService UserSessionService { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

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
            if (!await ConfirmEventChangeIfHistoryWillBeRemoved(upcomingEvent))
            {
                return;
            }

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

    async Task<bool> ConfirmEventChangeIfHistoryWillBeRemoved(UpcomingEvent upcomingEvent)
    {
        var session = await UserSessionService.GetCurrent();
        if (session?.SnapshotHistory.Count is not > 0 || session.EventId == null || session.EventId == upcomingEvent.Id)
        {
            return true;
        }

        var parameters = new DialogParameters<ChangeEventHistoryDialog> { { x => x.EventName, upcomingEvent.Name } };
        var dialog = await DialogService.ShowAsync<ChangeEventHistoryDialog>(Change_event_string, parameters);
        return !await dialog.IsCanceled();
    }
}
