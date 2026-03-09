using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Helpers;
using Not.Domain.Exceptions;
using NTS.Judge.Blazor.Features.Setup.StartValidation;
using NTS.Judge.Blazor.Layout.Drawer.Reset;
using NTS.Judge.Features.Core.State;

namespace NTS.Judge.Blazor.Layout.Drawer;

public class SideMenu2Behind : NStatefulComponent
{
    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    NavigationManager NavManager { get; set; } = default!;

    [Inject]
    ITimingStateService Service { get; set; } = default!;

    [Inject]
    ITimingStartService StartService { get; set; } = default!;

    protected bool IsStarted => Service.IsStarted;

    protected async Task Start()
    {
        try
        {
            var validation = StartService.Validate();
            if (validation.Data?.Any() == true)
            {
                var parameters = new DialogParameters<StartValidationDialog> { { x => x.InitialValidation, validation } };
                var dialog = await DialogService.ShowAsync<StartValidationDialog>(Start_string, parameters);
                var result = await dialog.Result;
                if (result == null || result.Canceled || result.Data is not true)
                {
                    return;
                }
            }

            await Service.StartTiming();
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

    protected async Task OpenSoftResetDialog()
    {
        try
        {
            var dialog = await DialogService.ShowAsync<SoftResetDialog>();
            if (await dialog.IsCanceled())
            {
                return;
            }
            NavManager.NavigateTo(Routes.HOME, forceLoad: true);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
