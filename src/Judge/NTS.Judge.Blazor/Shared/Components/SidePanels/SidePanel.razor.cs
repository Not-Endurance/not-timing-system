using MudBlazor;
using Not.Blazor.Dialogs;

namespace NTS.Judge.Blazor.Shared.Components.SidePanels;

public partial class SidePanel
{
    [Inject]
    ICoreBehind CoreBehind { get; set; } = default!;
    
    [Inject]
    IDialogService DialogService { get; set; } = default!;
    
    [Inject]
    NavigationManager NavManager { get; set; } = default!;
    
    protected override async Task OnInitializedAsync()
    {
        await Observe(CoreBehind);
    }

    public async Task Start()
    {
        await CoreBehind.Start();
    }

    async Task OpenResetCompetitionDialog()
    {
        var dialog = await DialogService.ShowAsync<ResetCompetitionsDialog>();
        if (await dialog.IsCanceled())
        {
            return;
        }

        await CoreBehind.Reset();
        NavManager.NavigateTo(NavManager.Uri, forceLoad: true);
    }
}
