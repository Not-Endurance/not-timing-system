using Microsoft.AspNetCore.Components;
using NC.Blazor.Dialogs.Abstractions;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Blazor.Components.SelectEvents;

public class SelectEventDialogBehind : NDialog
{
    [Inject]
    IEventInformationService EventInformationService { get; set; } = default!;

    [Inject]
    INoTimingSocketService SocketService { get; set; } = default!;

    protected IEnumerable<EventInformation> Events { get; set; } = [];
    protected string[] EventsTableHeaders { get; set; } = [Event_string];
    protected bool IsConnected => SocketService.IsConnected;
    protected EventInformation? SelectedEvent => SocketService.Event;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            Events = await EventInformationService.GetActive();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task ConnectTo(EventInformation eventInformation)
    {
        try
        {
            if (SocketService.IsConnected && SelectedEvent?.Id != eventInformation.Id)
            {
                await SocketService.Disconnect();
            }

            await SocketService.Connect(eventInformation);
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
