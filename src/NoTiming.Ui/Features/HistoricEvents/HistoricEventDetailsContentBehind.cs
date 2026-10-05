using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Components.Abstractions;
using NoTiming.Ui.Components.HistoricEvents;
using NTS.Contracts.HistoricEvents;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;

namespace NoTiming.Ui.Features.HistoricEvents;

public class HistoricEventDetailsContentBehind : NStatefulComponent
{
    [Inject]
    IHistoricEventService Service { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    protected bool IsEmpty => Service.Event == null || Service.Document == null;
    protected bool HasStartlist => Service.StartlistHistoryByStage.Count != 0;
    protected ResultsDocument? Document => Service.Document;
    protected IReadOnlyList<Ranking> Rankings => Service.Rankings;
    protected Ranking? CurrentRanking => Service.CurrentRanking;

    [Parameter]
    public Guid EventId { get; set; }

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
            var parameters = new DialogParameters<HistoricEventStartlistDialog>
            {
                { x => x.HistoryByStage, Service.StartlistHistoryByStage },
            };
            var options = new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Large };
            var dialog = await DialogService.ShowAsync<HistoricEventStartlistDialog>(
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
