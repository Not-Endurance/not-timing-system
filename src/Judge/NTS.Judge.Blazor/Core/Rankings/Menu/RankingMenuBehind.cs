using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.Dialogs;
using Not.Safe;
using Not.Structures;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Blazor.Core.Rankings.Menu;

public class RankingMenuBehind : NComponent
{
    [Inject]
    IRankingMenuService Service { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    public Ranking? SelectedRanking => Service.SelectedRanking;
    public ObservableList<Ranking> Rankings => Service.Rankings;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }

    public async Task Select(Ranking? ranking)
    {
        if (ranking == null)
        {
            return;
        }
        await SafeHelper.Run(() => Service.Select(ranking));
    }

    public async Task OpenDeleteDialog(MudChip<Ranking> chip)
    {
        var ranking = chip.Value!;
        var arguments = new DialogParameters<NConfirmDeleteDialog> { { x => x.Item, ranking.Name } };
        var dialog = await DialogService.ShowAsync<NConfirmDeleteDialog>(Delete_string, arguments);
        if (await dialog.IsCanceled())
        {
            return;
        }
        await SafeHelper.Run(() => Service.Delete(ranking));
    }
}
