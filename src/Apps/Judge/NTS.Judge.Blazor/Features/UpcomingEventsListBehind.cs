using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Helpers;
using Not.Domain.Exceptions;
using NTS.Application.Core;
using NTS.Application.Socket;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Features.Setup.StartValidation;
using NTS.Judge.Blazor.Layout.Drawer.Reset;
using NTS.Judge.Features.Core;

namespace NTS.Judge.Blazor.Features;

public class UpcomingEventsListBehind : NStatefulComponent
{
    [Inject]
    protected IDialogService DialogService { get; set; } = default!;

    [Inject]
    protected IDashService Service { get; set; } = default!;

    [Inject]
    protected INtsSocketService SocketService { get; set; } = default!;

    [Inject]
    protected IEnduranceEventService EnduranceEventService { get; set; } = default!;

    protected int ActiveEnduranceEventCount { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await Observe(SocketService);
        await RefreshActiveEventCount();
    }

    protected bool ShowStartButton()
    {
        return ActiveEnduranceEventCount == 0 && !SocketService.IsConnected;
    }

    protected bool ShowResetTimingButton(UpcomingEvent upcomingEvent)
    {
        return SocketService.Event?.Id == upcomingEvent.Id;
    }

    protected async Task Start(UpcomingEvent upcomingEvent)
    {
        try
        {
            var validation = await Service.Validate(upcomingEvent.Id);
            if (validation.Data?.Any() == true)
            {
                var parameters = new DialogParameters<StartValidationDialog>
                {
                    { x => x.InitialValidation, validation },
                    { x => x.UpcomingEventId, upcomingEvent.Id },
                };
                var dialog = await DialogService.ShowAsync<StartValidationDialog>(Start_string, parameters);
                if (await dialog.IsCanceled())
                {
                    return;
                }
            }

            await Service.Start(upcomingEvent.Id);
            await RefreshActiveEventCount();
        }
        catch (DomainException ex)
        {
            await DialogService.ShowMessageBox(Start_string, ex.Message);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OpenResetTimingDialog(UpcomingEvent upcomingEvent)
    {
        if (!ShowResetTimingButton(upcomingEvent))
        {
            return;
        }

        try
        {
            var dialog = await DialogService.ShowAsync<ResetTimingDialog>();
            if (await dialog.IsCanceled())
            {
                return;
            }

            await Service.Reset();
            await SocketService.Disconnect();
            await RefreshActiveEventCount();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    async Task RefreshActiveEventCount()
    {
        ActiveEnduranceEventCount = (await EnduranceEventService.GetEvents()).Count();
    }
}
