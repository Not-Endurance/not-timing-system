using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Objects;

namespace NTS.Blazor.Components.Results;

public class ResultsRowSummaryBehind : NComponent
{
    protected const Align SummaryAlign = Align.Center;
    protected override bool ObserveBreakpointChanges => true;
    protected bool UseInlineLayout => !IsMdAndDown;
    protected string SummaryClass => UseInlineLayout ? "results-row-inline-summary" : "results-row-side-summary";
    protected Total? Total => Participation.GetTotal();
    protected Timestamp? TotalArriveTime =>
        Participation.IsComplete()
            ? Total?.FinishTime
            : Participation.Phases.LastOrDefault(x => x.IsComplete())?.ArriveTime;
    protected bool ShowSummary => Participation.IsEliminated() || (ShowTotals && Total != null);

    [Parameter, EditorRequired]
    public Participation Participation { get; set; } = default!;

    [Parameter]
    public bool ShowTotals { get; set; } = true;
}
