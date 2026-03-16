using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Helpers;
using Not.Domain.Exceptions;
using NTS.Judge.Blazor.Features.Setup.StartValidation;
using NTS.Judge.Blazor.Layout.Drawer.Reset;
using NTS.Judge.Features.Core;
using NTS.Judge.Features.Socket;

namespace NTS.Judge.Blazor.Layout.Drawer;

public class EventMenuBehind : NStatefulComponent
{
    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    NavigationManager NavManager { get; set; } = default!;

    [Inject]
    IDashService Service { get; set; } = default!;

    [Inject]
    JudgeSocketService SocketContext { get; set; } = default!;

    protected bool IsEventStarted => Service.IsStarted;
    protected bool IsEventSelected => SocketContext.Event != null;

    protected async Task Start()
    {
        try
        {
            var validation = await Service.Start();
            if (validation.Data?.Any() == true)
            {
                var parameters = new DialogParameters<StartValidationDialog>
                {
                    { x => x.InitialValidation, validation },
                };
                var dialog = await DialogService.ShowAsync<StartValidationDialog>(Start_string, parameters);
                if (await dialog.IsCanceled())
                {
                    return;
                }
                await Service.Start();
            }
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

    protected async Task OpenDisconnectDialog()
    {
        try
        {
            var dialog = await DialogService.ShowAsync<DisconnectEventDialog>();
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
        await Observe(SocketContext);
    }
}
