using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Dialogs.Abstractions;
using Not.Blazor.Helpers;
using NTS.Application.Core;
using NTS.Application.Socket;
using NTS.Domain.Core.Aggregates;

namespace NTS.Blazor.Components.SelectEvents;

public class SelectEventDialogBehind : NDialog
{
    [Inject]
    IEnduranceEventService EnduranceEventService { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    protected IEnumerable<EnduranceEvent> Events { get; set; } = [];
    protected string[] EventsTableHeaders { get; set; } = [Event_string];
    protected bool IsConnected => SocketService.IsConnected;
    protected EnduranceEvent? SelectedEvent => SocketService.Event;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            Events = await EnduranceEventService.GetEvents();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task ConnectTo(EnduranceEvent enduranceEvent)
    {
        try
        {
            if (!await ConfirmSessionResetIfNeeded(enduranceEvent))
            {
                return;
            }

            if (SocketService.IsConnected && SelectedEvent?.Id != enduranceEvent.Id)
            {
                await SocketService.Disconnect();
            }

            await SocketService.Connect(enduranceEvent);
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

    async Task<bool> ConfirmSessionResetIfNeeded(EnduranceEvent enduranceEvent)
    {
        if (!await SocketService.WillResetSession(enduranceEvent))
        {
            return true;
        }

        var parameters = new DialogParameters<ChangeEventHistoryDialog>
        {
            { x => x.EventName, enduranceEvent.PopulatedPlace.City },
        };
        var dialog = await DialogService.ShowAsync<ChangeEventHistoryDialog>(Change_event_string, parameters);
        return !await dialog.IsCanceled();
    }
}
