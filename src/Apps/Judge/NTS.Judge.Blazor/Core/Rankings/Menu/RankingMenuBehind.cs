using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.Dialogs;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Features.Core.Rankings;

namespace NTS.Judge.Blazor.Core.Rankings.Menu;

public class RankingMenuBehind : NComponent
{
    [Inject]
    IRankingMenuService RankingMenuService { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    protected Ranking? Current { get; private set; }
    protected ObservableList<Ranking> Rankings => RankingMenuService.Rankings;

    protected override async Task OnInitializedAsync()
    {
        await Observe(RankingMenuService);
    }

    protected override void OnBeforeRender()
    {
        Current = RankingMenuService.Current;
    }

    protected void Select(Ranking ranking)
    {
        try
        {
            RankingMenuService.Select(ranking);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OpenDeleteDialog(MudChip<Ranking> chip)
    {
        try
        {
            var ranking = chip.Value!;
            var arguments = new DialogParameters<NConfirmDeleteDialog> { { x => x.Item, ranking.Name } };
            var dialog = await DialogService.ShowAsync<NConfirmDeleteDialog>(Delete_string, arguments);
            if (await dialog.IsCanceled())
            {
                return;
            }
            await RankingMenuService.Delete(ranking);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
