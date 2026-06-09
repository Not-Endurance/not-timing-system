using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Aggregates.Results;

namespace NTS.Blazor.Components.Results;

public class ParticipationResultComponentBehind : NComponent
{
    protected bool HasRank => Entry.Rank != null;
    protected bool ShowTotals => Entry.Rank != null;
    protected override bool ObserveBreakpointChanges => true;
    protected bool UseInlineLayout => !IsMdAndDown;

    [Parameter, EditorRequired]
    public ParticipationResult Entry { get; set; } = default!;

    protected string GetRankText()
    {
        if (Entry.Participation.IsEliminated())
        {
            return " ";
        }
        return Entry.Rank?.ToString() ?? string.Empty;
    }
}
