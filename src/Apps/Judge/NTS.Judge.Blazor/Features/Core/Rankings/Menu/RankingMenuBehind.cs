using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Dialogs;
using Not.Blazor.Mud;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Features.Core.Rankings;

namespace NTS.Judge.Blazor.Features.Core.Rankings.Menu;

public class RankingMenuBehind : NStatefulComponent
{
    [Inject]
    IDialogService DialogService { get; set; } = default!;

    protected ObservableList<Ranking> Rankings => Service.Rankings;

    [Inject]
    protected IRankingMenuService Service { get; set; } = default!;

    protected void Select(Ranking ranking)
    {
        try
        {
            Service.Select(ranking);
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
            await Service.Delete(ranking);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
