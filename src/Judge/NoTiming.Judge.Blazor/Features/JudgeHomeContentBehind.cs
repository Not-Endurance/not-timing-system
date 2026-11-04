using MudBlazor;
using NC.Blazor.Components.Abstractions;
using NC.Blazor.Helpers;
using NC.Domain.Exceptions;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Judge.Blazor.Features.Setup.StartValidation;
using NoTiming.Judge.Blazor.Layout.Drawer.Reset;
using NoTiming.Judge.Contracts.Features.Core;

namespace NoTiming.Judge.Blazor.Features;

public class JudgeHomeContentBehind : NStatefulComponent
{
    [Inject]
    protected IDialogService DialogService { get; set; } = default!;

    [Inject]
    protected IDashService DashService { get; set; } = default!;

    [Inject]
    protected INoTimingSocketService SocketService { get; set; } = default!;

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
