using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Helpers;
using Not.Domain.Exceptions;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Features.Setup.StartValidation;
using NTS.Judge.Blazor.Layout.Drawer.Reset;
using NTS.Judge.Contracts.Features.Core;

namespace NTS.Judge.Blazor.Features;

public class HomeContentBehind : NStatefulComponent
{
    [Inject]
    protected IDialogService DialogService { get; set; } = default!;

    [Inject]
    protected IDashService DashService { get; set; } = default!;

    [Inject]
    protected INtsSocketService SocketService { get; set; } = default!;

    [Inject]
    protected IActiveEventsContext ActiveEventContext { get; set; } = default!;

    protected int ActiveEventInformationCount { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await Observe(SocketService);
        await Observe(ActiveEventContext);
    }

    protected bool ShowStartButton(ConfigureEvent configureEvent)
    {
        return !ActiveEventContext.IsActive(configureEvent);
    }

    protected bool ShowEditButton(ConfigureEvent configureEvent)
    {
        return ShowStartButton(configureEvent) && !ShowResetTimingButton(configureEvent);
    }

    protected bool ShowViewButton(ConfigureEvent configureEvent)
    {
        return ActiveEventContext.IsActive(configureEvent);
    }

    protected bool ShowResetTimingButton(ConfigureEvent configureEvent)
    {
        return SocketService.Event?.Id == configureEvent.Id;
    }

    protected async Task Start(ConfigureEvent configureEvent)
    {
        try
        {
            var validation = await DashService.Validate(configureEvent.Id);
            if (validation.Data?.Any() == true)
            {
                var parameters = new DialogParameters<StartValidationDialog>
                {
                    { x => x.InitialValidation, validation },
                    { x => x.ConfigureEventId, configureEvent.Id },
                };
                var dialog = await DialogService.ShowAsync<StartValidationDialog>(Start_string, parameters);
                if (await dialog.IsCanceled())
                {
                    return;
                }
            }

            await DashService.Start(configureEvent.Id);
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

    protected async Task OpenResetTimingDialog(ConfigureEvent configureEvent)
    {
        if (!ShowResetTimingButton(configureEvent))
        {
            return;
        }

        try
        {
            var dialog = await DialogService.ShowAsync<ResetEventDialog>();
            if (await dialog.IsCanceled())
            {
                return;
            }

            await DashService.Reset();
            await SocketService.Disconnect();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
