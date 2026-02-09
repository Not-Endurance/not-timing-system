using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.Mud;
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

    protected bool IsStarted => Service.IsStarted;

    protected async Task Start()
    {
        await Service.StartTiming();
    }

    protected async Task OpenSoftResetDialog()
    {
        var dialog = await DialogService.ShowAsync<SoftResetDialog>();
        if (await dialog.IsCanceled())
        {
            return;
        }
        NavManager.NavigateTo(Routes.HOME, forceLoad: true);
    }

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
