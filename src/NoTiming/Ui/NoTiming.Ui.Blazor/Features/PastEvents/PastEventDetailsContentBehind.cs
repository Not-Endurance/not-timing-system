using Microsoft.AspNetCore.Components;
using MudBlazor;
using NC.Blazor.Components.Abstractions;
using NoTiming.Application.Contracts.PastEvents;
using NoTiming.Blazor.Components.PastEvents;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Objects.Documents;

namespace NoTiming.Ui.Blazor.Features.PastEvents;

public class PastEventDetailsContentBehind : NStatefulComponent
{
    [Inject]
    IPastEventService Service { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    protected bool IsEmpty => Service.Event == null || Service.Document == null;
    protected bool HasStartlist => Service.StartlistHistoryByStage.Count != 0;
    protected ResultsDocument? Document => Service.Document;
    protected IReadOnlyList<Ranking> Rankings => Service.Rankings;
    protected Ranking? CurrentRanking => Service.CurrentRanking;

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
            var dialog = await DialogService.ShowAsync<PastEventStartlistDialog>(Startlist_string, parameters, options);
            await dialog.Result;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
