using MudBlazor;
using Not.Blazor.Components.Abstractions;
using NTS.Application.Contracts.PastEvents;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Blazor.Features.PastEvents;

public class PastEventDetailsContentBehind : NStatefulComponent
{
    [Inject]
    protected IPastEventService Service { get; set; } = default!;

    [Inject]
    protected IDialogService DialogService { get; set; } = default!;

    protected bool IsEmpty => Service.Event == null || Service.Document == null;
    protected bool HasStartlist => Service.StartlistHistoryByStage.Count != 0;

    [Parameter]
    public int EventId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await Service.LoadEvent(EventId);
            await Observe(Service);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void SelectRanking(Ranking ranking)
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

    protected async Task OpenStartlist()
    {
        try
        {
            var parameters = new DialogParameters<PastEventStartlistDialog>
            {
                { x => x.HistoryByStage, Service.StartlistHistoryByStage },
            };
            var options = new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Large };
            var dialog = await DialogService.ShowAsync<PastEventStartlistDialog>(
                Startlist_string,
                parameters,
                options
            );
            await dialog.Result;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
