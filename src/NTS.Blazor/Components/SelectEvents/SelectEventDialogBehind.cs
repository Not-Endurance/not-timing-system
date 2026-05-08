using Microsoft.AspNetCore.Components;
using Not.Blazor.Dialogs.Abstractions;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Aggregates;

namespace NTS.Blazor.Components.SelectEvents;

public class SelectEventDialogBehind : NDialog
{
    [Inject]
    IEventInformationService EventInformationService { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

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
